using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Execution;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Risk;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Strategy;

/// <summary>
/// Motor de estratégia para um único símbolo e uma estratégia configurada.
/// Consome ticks, detecta fechamento de candle, calcula indicadores,
/// avalia as regras de risco e delega execução ao ServicoExecucao.
/// </summary>
public sealed class StrategyEngine : IAsyncDisposable
{
    private readonly SymbolConfig _config;
    private EstrategiaConfig _estrategiaConfig;
    private readonly IGatewayMt5 _gateway;
    private readonly IConnectionManager _connectionManager;
    private readonly CatalogoIndicadores _catalogo;
    private readonly CatalogoIndicadoresMultiFonte _catalogoMultiFonte;
    private readonly CatalogoEstrategias _catalogoEstrategias;
    private readonly RiskGuard _riskGuard;
    private readonly CalculadoraLote _calculadoraLote;
    private readonly IServicoExecucao _execucao;
    private readonly ILogger<StrategyEngine> _logger;

    private List<CandleMt5> _candles = new();
    private DateTime _ultimoCandleFechado = DateTime.MinValue;
    private double _saldoInicialDia;
    private CancellationTokenSource? _cts;
    private Task? _loopTask;
    private TickSubscription? _tickSubscription;

    public StrategyEngine(
        SymbolConfig config,
        EstrategiaConfig estrategiaConfig,
        IGatewayMt5 gateway,
        IConnectionManager connectionManager,
        CatalogoIndicadores catalogo,
        CatalogoIndicadoresMultiFonte catalogoMultiFonte,
        CatalogoEstrategias catalogoEstrategias,
        RiskGuard riskGuard,
        CalculadoraLote calculadoraLote,
        IServicoExecucao execucao,
        ILogger<StrategyEngine> logger)
    {
        _config = config;
        _estrategiaConfig = estrategiaConfig;
        _gateway = gateway;
        _connectionManager = connectionManager;
        _catalogo = catalogo;
        _catalogoMultiFonte = catalogoMultiFonte;
        _catalogoEstrategias = catalogoEstrategias;
        _riskGuard = riskGuard;
        _calculadoraLote = calculadoraLote;
        _execucao = execucao;
        _logger = logger;
    }

    /// <summary>Inicia o loop de escuta de ticks e avaliação de candles.</summary>
    public async Task IniciarAsync(IMarketDataService marketData, CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var brokerSymbol = _config.BrokerSymbol ?? _config.Symbol;

        var subOk = await _gateway.SubscreverSimboloAsync(brokerSymbol, _cts.Token);
        if (!subOk)
        {
            _logger.LogWarning("[{Simbolo}|{Estrategia}] Falha ao subscrever o símbolo no Market Watch do MT5.", brokerSymbol, _estrategiaConfig.Nome);
        }

        var timeframe = _estrategiaConfig.Indicadores?.FirstOrDefault()?.Timeframe ?? "M1";
        
        var estrategia = _catalogoEstrategias.Resolver(_estrategiaConfig.Nome);
        var maxPeriodo = CalcularMaxPeriodoRequerido(_estrategiaConfig, estrategia);

        _candles = (await _gateway.ObterCandlesAsync(brokerSymbol, timeframe, maxPeriodo + 10, _cts.Token)).ToList();

        _logger.LogInformation("[{Simbolo}|{Estrategia}] Iniciado — {Candles} candles carregados ({Timeframe}). Magic={Magic}",
            brokerSymbol, _estrategiaConfig.Nome, _candles.Count, timeframe, _estrategiaConfig.MagicNumber);

        if (_candles.Any())
        {
            _ultimoCandleFechado = _candles.Last().Tempo;
        }

        var conta = await _gateway.ObterInfoContaAsync(_cts.Token);
        _saldoInicialDia = conta.Saldo;

        _loopTask = LoopAsync(marketData, timeframe, _cts.Token);
    }

    private async Task LoopAsync(IMarketDataService marketData, string timeframe, CancellationToken ct)
    {
        _tickSubscription = marketData.AssinarTicks();

        await foreach (var tick in _tickSubscription.Reader.ReadAllAsync(ct))
        {
            if (tick.TerminalId != _config.TerminalId || tick.Symbol != (_config.BrokerSymbol ?? _config.Symbol))
                continue;

            var novosCandles = await _gateway.ObterCandlesAsync(tick.Symbol, timeframe, 3, ct);
            var candleFechado = novosCandles.LastOrDefault();

            if (candleFechado is null) continue;

            if (candleFechado.Tempo == _ultimoCandleFechado)
                continue;

            _logger.LogInformation("[{Simbolo}|{Estrategia}] Novo candle fechado detectado: {Tempo}. Rodando avaliação.",
                tick.Symbol, _estrategiaConfig.Nome, candleFechado.Tempo);

            _ultimoCandleFechado = candleFechado.Tempo;

            if (!_candles.Any(c => c.Tempo == candleFechado.Tempo))
            {
                _candles.Add(candleFechado);

                var estrategiaLocal = _catalogoEstrategias.Resolver(_estrategiaConfig.Nome);
                var limiteCandles = CalcularMaxPeriodoRequerido(_estrategiaConfig, estrategiaLocal) + 50;
                
                if (_candles.Count > limiteCandles)
                {
                    _candles.RemoveRange(0, _candles.Count - limiteCandles);
                }
            }

            await AvaliarEntradaAsync(tick, ct);
        }
    }

    private async Task AvaliarEntradaAsync(Domain.Events.TickEvent tick, CancellationToken ct)
    {
        if (!_config.Operar || !_estrategiaConfig.Ativa) return;

        var estrategia = _catalogoEstrategias.Resolver(_estrategiaConfig.Nome);
        if (estrategia == null)
        {
            _logger.LogWarning("[{Simbolo}|{Estrategia}] Estratégia não encontrada no catálogo. Desativando-a para evitar flood de logs.", _config.Symbol, _estrategiaConfig.Nome);
            _estrategiaConfig = _estrategiaConfig with { Ativa = false };
            return;
        }

        var brokerSymbol = _config.BrokerSymbol ?? _config.Symbol;

        var indicadoresCalc = await CalcularIndicadoresAsync(brokerSymbol, ct);

        // MultiFonte configuration (legacy, uses _config root for now, or adapt if needed in EstrategiaConfig)
        var indicadoresMultiFonte = await _catalogoMultiFonte.CalcularTodosAsync(_config.IndicadoresMultiFonte ?? [], _connectionManager.GetAllGateways(), ct);

        // Avaliação via componente isolado
        var decisao = estrategia.Avaliar(_candles, indicadoresCalc, indicadoresMultiFonte, tick, _estrategiaConfig);

        if (!decisao.Executar)
        {
            _logger.LogDebug("[{Simbolo}|{Estrategia}] Aguardando: {Motivo}", brokerSymbol, _estrategiaConfig.Nome, decisao.Motivo);
            return;
        }

        // --- Checagens de risco (Gestão de Risco da Estratégia e Agregada) ---
        var conta = await _gateway.ObterInfoContaAsync(ct);

        // 1. Drawdown Global da Conta (Agregado)
        if (_config.DrawdownDiarioMaximoAgregadoPercent.HasValue)
        {
            if (!_riskGuard.ValidarDrawdownDiario(
                _config.DrawdownDiarioMaximoAgregadoPercent.Value,
                _saldoInicialDia,
                conta.Equidade,
                brokerSymbol,
                "Agregado (Global)"))
                return;
        }

        // 2. Trava de Horário (Estratégia)
        if (!_riskGuard.ValidarJanelaHorario(_estrategiaConfig.JanelaHorarioPermitido, DateTime.UtcNow)) return;

        // 3. Drawdown da Estratégia (Individual)
        if (_estrategiaConfig.GestaoDeRisco?.DrawdownDiarioMaximoPercent is double drawdownLimit)
        {
            var hoje = DateTime.UtcNow.Date;
            var lucroFechado = await _gateway.ObterLucroPrejuizoDiaAsync(brokerSymbol, _estrategiaConfig.MagicNumber, hoje, ct);
            var lucroAberto = await _gateway.ObterLucroAbertoAsync(brokerSymbol, _estrategiaConfig.MagicNumber, ct);
            var pnlIsolado = lucroFechado + lucroAberto;

            if (!_riskGuard.ValidarDrawdownIsolado(
                drawdownLimit,
                _saldoInicialDia,
                pnlIsolado,
                brokerSymbol,
                $"Estratégia: {_estrategiaConfig.Nome} | Magic: {_estrategiaConfig.MagicNumber}"))
                return;
        }

        // 3.5. Cooldown Pós-Fechamento
        if (_estrategiaConfig.Saida?.CooldownAposFechamentoSegundos is int cooldownSegundos && cooldownSegundos > 0)
        {
            var ultimoFechamentoUtc = await _gateway.ObterMomentoUltimoFechamentoAsync(brokerSymbol, _estrategiaConfig.MagicNumber, ct);
            if (ultimoFechamentoUtc.HasValue)
            {
                var tempoDesdeUltimoFechamento = DateTime.UtcNow - ultimoFechamentoUtc.Value;
                if (tempoDesdeUltimoFechamento.TotalSeconds < cooldownSegundos)
                {
                    _logger.LogDebug("[{Simbolo}|{Estrategia}] Aguardando: Cooldown ativo. Restam {Restam:N0}s.",
                        brokerSymbol, _estrategiaConfig.Nome, cooldownSegundos - tempoDesdeUltimoFechamento.TotalSeconds);
                    return;
                }
            }
        }

        // 3.6. Máximo de Stops Consecutivos
        var maxStops = ParametroParser.ObterInt(_estrategiaConfig.Parametros, "maxStopsConsecutivos", 0);
        if (maxStops > 0)
        {
            var consecStops = await _gateway.ObterStopsConsecutivosAsync(brokerSymbol, _estrategiaConfig.MagicNumber, ct);
            if (consecStops >= maxStops)
            {
                _logger.LogInformation("[{Simbolo}|{Estrategia}] Aguardando: Limite de stops consecutivos atingido ({Stops} >= {Max}).", brokerSymbol, _estrategiaConfig.Nome, consecStops, maxStops);
                return;
            }
        }

        // 4. Limite de Operações Simultâneas (Por MagicNumber)
        var maxOperacoes = _estrategiaConfig.GestaoDeRisco?.MaxOperacoesSimultaneas ?? 1;
        var posicoesAbertas = await _gateway.ObterTicketsPosicoesAbertasAsync(brokerSymbol, _estrategiaConfig.MagicNumber, ct);
        if (!_riskGuard.ValidarMaxOperacoes(maxOperacoes, posicoesAbertas.Count, brokerSymbol, $"Magic={_estrategiaConfig.MagicNumber}")) return;

        // 5. Validação SL vs Spread
        if (_estrategiaConfig.Saida?.StopLossAtrMultiplo.HasValue == true)
        {
            var atrResult = indicadoresCalc.FirstOrDefault(r => r.Config.Nome.Equals("ATR", StringComparison.OrdinalIgnoreCase));
            var atrAtual = atrResult.Resultado?.Valor ?? 0;
            var tickAtual = await _gateway.ObterTickAtualAsync(brokerSymbol, ct);
            var spread = tickAtual.Ask - tickAtual.Bid;

            if (!_riskGuard.ValidarSlVsSpread(
                _estrategiaConfig.Saida.StopLossAtrMultiplo,
                _estrategiaConfig.Saida.SlMinimoSobreSpread,
                atrAtual,
                spread,
                brokerSymbol))
                return;
        }

        // --- Calcula volume e níveis ---
        var tickPreco = await _gateway.ObterTickAtualAsync(brokerSymbol, ct);
        var (sl, tp) = await CalcularSlTpAsync(decisao.Lado!.Value, tickPreco, indicadoresCalc, ct, decisao.StopSugerido);

        if (!sl.HasValue || sl.Value <= 0)
        {
            _logger.LogWarning(
                "[{Simbolo}|{Estrategia}] Entrada bloqueada: Stop Loss obrigatório não foi calculado.",
                brokerSymbol, _estrategiaConfig.Nome);
            return;
        }

        var precoEntrada = decisao.Lado == LadoOrdem.Compra ? tickPreco.Ask : tickPreco.Bid;
        var distanciaStop = Math.Abs(precoEntrada - sl.Value);

        var volume = await _calculadoraLote.CalcularAsync(
            _estrategiaConfig.GestaoDeRisco?.ModoLote ?? "loteFixo",
            _estrategiaConfig.GestaoDeRisco?.LoteFixo,
            _estrategiaConfig.GestaoDeRisco?.PercentualConta,
            conta.Equidade,
            precoEntrada,
            distanciaStop,
            _gateway,
            brokerSymbol,
            ct);

        var comentarioOrdem = $"financial.robot|{_estrategiaConfig.Nome}";

        // --- Executa ---
        _logger.LogInformation("[{Simbolo}|{Estrategia}] Iniciando Execução: Lado={Lado}, Volume={Volume}, SL={SL}, TP={TP}",
            brokerSymbol, _estrategiaConfig.Nome, decisao.Lado, volume, sl, tp);

        await _execucao.AbrirPosicaoAsync(
            _config.TerminalId,
            brokerSymbol,
            decisao.Lado!.Value,
            volume,
            sl,
            tp,
            _estrategiaConfig.MagicNumber,
            comentarioOrdem,
            ct);
    }

    private async Task<(double? sl, double? tp)> CalcularSlTpAsync(
        LadoOrdem lado,
        Domain.Interfaces.TickMt5 tick,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        CancellationToken ct,
        double? stopEstrutural = null)
    {
        var saida = _estrategiaConfig.Saida;
        if (saida is null && stopEstrutural is null) return (null, null);

        var preco = lado == LadoOrdem.Compra ? tick.Ask : tick.Bid;
        double? slDist = null;
        double? tpDist = null;

        // Stop estrutural sugerido pela própria estratégia de entrada (ex: barra de sinal em Price Action)
        // tem prioridade sobre o SL genérico do SaidaConfig. O TP continua vindo da config, agora como
        // múltiplo desse risco real — permite um TP "mais longo" por segurança sem inventar alvo técnico.
        if (stopEstrutural.HasValue)
        {
            slDist = Math.Abs(preco - stopEstrutural.Value);
            if (saida?.TakeProfitAtrMultiplo is decimal tpAtrMult)
            {
                var atrEstrutural = indicadores.FirstOrDefault(r => r.Config.Nome.Equals("ATR", StringComparison.OrdinalIgnoreCase)).Resultado?.Valor ?? 0;
                if (atrEstrutural > 0)
                {
                    tpDist = (double)tpAtrMult * atrEstrutural;
                }
                else
                {
                    _logger.LogWarning(
                        "[{Simbolo}|{Estrategia}] TakeProfitAtrMultiplo={Mult} configurado, mas nenhum indicador ATR disponível para esta estratégia. TP caindo para 2x o risco estrutural em vez do múltiplo configurado.",
                        _config.Symbol, _estrategiaConfig.Nome, tpAtrMult);
                    tpDist = slDist * 2;
                }
            }
            else if (saida?.TakeProfitPips is decimal tpPips)
            {
                tpDist = (double)tpPips;
            }
            else
            {
                tpDist = slDist * 2;
            }
        }
        // SL/TP via ATR
        else if (saida!.StopLossAtrMultiplo.HasValue)
        {
            var atr = indicadores.FirstOrDefault(r => r.Config.Nome.Equals("ATR", StringComparison.OrdinalIgnoreCase)).Resultado?.Valor ?? 0;
            slDist = (double)saida.StopLossAtrMultiplo.Value * atr;
            tpDist = saida.TakeProfitAtrMultiplo.HasValue
                ? (double)saida.TakeProfitAtrMultiplo.Value * atr
                : slDist * 2;
        }
        // SL/TP via percentual sobre o preço
        else if (saida.StopLossPercentualPreco.HasValue)
        {
            var precoSl = Execution.CalculoSaidaParametrica.CalcularPrecoPercentual(lado == LadoOrdem.Compra, preco, saida.StopLossPercentualPreco.Value, true);
            slDist = Math.Abs(preco - precoSl);
            
            if (saida.TakeProfitPercentualPreco.HasValue)
            {
                var precoTp = Execution.CalculoSaidaParametrica.CalcularPrecoPercentual(lado == LadoOrdem.Compra, preco, saida.TakeProfitPercentualPreco.Value, false);
                tpDist = Math.Abs(preco - precoTp);
            }
            else
            {
                tpDist = slDist * 2;
            }
        }
        // SL/TP via preço absoluto (pips)
        else if (saida.StopLossPips.HasValue)
        {
            slDist = (double)saida.StopLossPips.Value;
            tpDist = saida.TakeProfitPips.HasValue ? (double)saida.TakeProfitPips.Value : slDist * 2;
        }

        if (slDist.HasValue)
        {
            var ponto = await _gateway.ObterTamanhoPontoAsync(_config.BrokerSymbol ?? _config.Symbol, ct);
            var stopsLevel = await _gateway.ObterStopsLevelAsync(_config.BrokerSymbol ?? _config.Symbol, ct);
            var minDist = stopsLevel * ponto;

            if (minDist > 0)
            {
                if (slDist.Value < minDist)
                {
                    _logger.LogInformation("[{Simbolo}|{Estrategia}] SL ajustado para o nível mínimo da corretora ({MinDist:F5}).", _config.Symbol, _estrategiaConfig.Nome, minDist);
                    slDist = minDist;
                }
                if (tpDist.HasValue && tpDist.Value < minDist)
                {
                    _logger.LogInformation("[{Simbolo}|{Estrategia}] TP ajustado para o nível mínimo da corretora ({MinDist:F5}).", _config.Symbol, _estrategiaConfig.Nome, minDist);
                    tpDist = minDist;
                }
            }

            var slCalculado = lado == LadoOrdem.Compra ? preco - slDist.Value : preco + slDist.Value;
            var tpCalculado = tpDist.HasValue
                ? (lado == LadoOrdem.Compra ? preco + tpDist.Value : preco - tpDist.Value)
                : (double?)null;

            return (
                NormalizarAoTick(slCalculado, ponto),
                tpCalculado.HasValue ? NormalizarAoTick(tpCalculado.Value, ponto) : null
            );
        }

        return (null, null);
    }

    private async Task<IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)>> CalcularIndicadoresAsync(
        string brokerSymbol,
        CancellationToken ct)
    {
        var indicadores = _estrategiaConfig.Indicadores ?? [];
        if (indicadores.Count == 0)
            return [];

        var timeframeBase = indicadores.FirstOrDefault()?.Timeframe ?? "M1";
        var resultados = new List<(IndicadorConfig Config, ResultadoIndicador Resultado)>();

        foreach (var grupo in indicadores.GroupBy(i => i.Timeframe, StringComparer.OrdinalIgnoreCase))
        {
            var candles = grupo.Key.Equals(timeframeBase, StringComparison.OrdinalIgnoreCase)
                ? _candles
                : await ObterCandlesParaIndicadoresAsync(brokerSymbol, grupo, ct);

            resultados.AddRange(_catalogo.CalcularTodos(grupo, candles));
        }

        return resultados;
    }

    private async Task<IReadOnlyList<CandleMt5>> ObterCandlesParaIndicadoresAsync(
        string brokerSymbol,
        IEnumerable<IndicadorConfig> indicadores,
        CancellationToken ct)
    {
        var lista = indicadores.ToList();
        var timeframe = lista.First().Timeframe;
        var maxPeriodo = lista
            .Select(i => ParametroParser.ObterInt(i.Parametros, "periodo", 14))
            .DefaultIfEmpty(14)
            .Max();

        return await _gateway.ObterCandlesAsync(brokerSymbol, timeframe, maxPeriodo + 60, ct);
    }

    public static int CalcularMaxPeriodoRequerido(EstrategiaConfig config, IEstrategiaEntrada? estrategia)
    {
        var maxPeriodoIndicadores = 50;
        if (config.Indicadores is not null && config.Indicadores.Any())
        {
            maxPeriodoIndicadores = config.Indicadores
                .Select(i => ParametroParser.ObterInt(i.Parametros, "periodo", 14))
                .Max() + 10;
        }
        var maxPeriodoEstrategia = estrategia?.ObterLookbackNecessario(config) ?? 0;
        return Math.Max(maxPeriodoIndicadores, maxPeriodoEstrategia);
    }

    private static double NormalizarAoTick(double valor, double ponto)
    {
        if (ponto <= 0) return valor;
        return Math.Round(valor / ponto, MidpointRounding.AwayFromZero) * ponto;
    }

    public async ValueTask DisposeAsync()
    {
        _cts?.Cancel();
        try
        {
            if (_loopTask is not null)
                await _loopTask.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Ignorado
        }
        finally
        {
            _tickSubscription?.Dispose();
            _cts?.Dispose();
        }
    }
}
