using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Config;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Execution;

/// <summary>
/// Fase 8 — percorre periodicamente as posições abertas de todas as estratégias ativas
/// e aplica breakeven + trailing stop conforme configurado em cada SaidaConfig.
/// Também dispara saídas parciais (scale-out) por alvo de lucro em preço e, quando configurado,
/// reposiciona o break-even a cada parcial executada.
/// Nunca fecha posições integralmente — só ajusta SL via ModificarPosicaoAsync e reduz volume via FecharPosicaoParcialAsync.
/// </summary>
public sealed class GerenciadorPosicoesAbertasService : BackgroundService
{
    private readonly ConfigWatcherService _configWatcher;
    private readonly IConnectionManager _connectionManager;
    private readonly IServicoExecucao _execucao;
    private readonly ILogger<GerenciadorPosicoesAbertasService> _logger;

    private static readonly TimeSpan IntervaloVerificacao = TimeSpan.FromSeconds(10);

    // Estado em memória de quantas saídas parciais já foram executadas por ticket, junto com o
    // grupo (terminal/símbolo/magic) a que pertence — necessário para reconciliar o ticket contra o
    // broker mesmo em ciclos em que sua estratégia não tem breakeven/trailing/parciais ativos (ver
    // ReconciliarTicketsPendentesAsync). Reseta ao reiniciar o serviço.
    private readonly Dictionary<ulong, EstadoParcial> _estadoParciais = new();
    private readonly Dictionary<ulong, DateTime> _ultimoCandleFechadoAvaliado = new();

    private sealed record EstadoParcial(int ProximoIndice, string TerminalId, string Simbolo, long MagicNumber);

    private readonly CatalogoEstrategias _catalogoEstrategias;
    private readonly CatalogoIndicadores _catalogoIndicadores;
    private readonly CatalogoIndicadoresMultiFonte _catalogoMultiFonte;

    public GerenciadorPosicoesAbertasService(
        ConfigWatcherService configWatcher,
        IConnectionManager connectionManager,
        IServicoExecucao execucao,
        CatalogoEstrategias catalogoEstrategias,
        CatalogoIndicadores catalogoIndicadores,
        CatalogoIndicadoresMultiFonte catalogoMultiFonte,
        ILogger<GerenciadorPosicoesAbertasService> logger)
    {
        _configWatcher = configWatcher;
        _connectionManager = connectionManager;
        _execucao = execucao;
        _catalogoEstrategias = catalogoEstrategias;
        _catalogoIndicadores = catalogoIndicadores;
        _catalogoMultiFonte = catalogoMultiFonte;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(IntervaloVerificacao);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await ProcessarTodasAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GestaoDinamica] Erro no ciclo de verificação.");
            }
        }
    }

    private async Task ProcessarTodasAsync(CancellationToken ct)
    {
        var ticketsVistos = new HashSet<ulong>();
        var gruposConsultados = new HashSet<(string TerminalId, string Simbolo, long MagicNumber)>();

        var saldosPorTerminal = new Dictionary<string, double?>();

        foreach (var config in _configWatcher.GetActiveConfigs())
        {
            if (!config.Operar) continue;

            var estrategias = config.Estrategias is { Count: > 0 }
                ? config.Estrategias
                : new List<EstrategiaConfig> { CriarEstrategiaFallback(config) };

            foreach (var est in estrategias.Where(e => e.Ativa))
            {
                var saida = est.Saida ?? config.Saida;
                
                var estrategiaObj = _catalogoEstrategias.Resolver(est.Nome);
                var temSaidaTecnica = estrategiaObj is IEstrategiaSaida;

                var temBreakeven = saida?.BreakevenGatilhoAtrMultiplo is > 0;
                var temTrailing = saida?.TrailingStopPips is > 0 || saida?.TrailingStopAtrMultiplo is > 0 || saida?.TrailingStopPercentualPreco is > 0;
                var temParciais = saida?.SaidasParciais is { Count: > 0 };
                var temSaidaParametrica = saida?.TakeProfitPercentualConta is > 0 || saida?.StopLossPercentualConta is > 0 ||
                                          saida?.TakeProfitValorBruto is > 0 || saida?.StopLossValorBruto is > 0 ||
                                          saida?.EncerrarAtivaAcimaDe.HasValue == true || saida?.EncerrarAtivaAbaixoDe.HasValue == true;

                if (!temBreakeven && !temTrailing && !temParciais && !temSaidaParametrica && !temSaidaTecnica) continue;

                await ProcessarEstrategiaAsync(config, est, saida, estrategiaObj, ticketsVistos, gruposConsultados, saldosPorTerminal, ct);
            }
        }

        // Reconcilia tickets com progresso de parcial pendente cujo grupo (terminal/símbolo/magic)
        // não foi consultado neste ciclo — por exemplo, se a config foi recarregada momentaneamente
        // sem breakeven/trailing/parciais. Evita podar (e assim reiniciar) o progresso de um ticket
        // que só não apareceu porque sua estratégia não foi processada, não porque fechou de fato.
        await ReconciliarTicketsPendentesAsync(ticketsVistos, gruposConsultados, ct);

        PodarEstadoParciais(ticketsVistos, gruposConsultados);
    }

    private async Task ProcessarEstrategiaAsync(
        SymbolConfig config, EstrategiaConfig est, SaidaConfig? saida, IEstrategiaEntrada? estrategiaObj,
        HashSet<ulong> ticketsVistos, HashSet<(string TerminalId, string Simbolo, long MagicNumber)> gruposConsultados,
        Dictionary<string, double?> saldosPorTerminal,
        CancellationToken ct)
    {
        IGatewayMt5 gateway;
        try
        {
            gateway = _connectionManager.GetClient(config.TerminalId);
        }
        catch
        {
            _logger.LogWarning("[GestaoDinamica] {Simbolo}/{Estrategia}: terminal {Terminal} não conectado, pulando ciclo.",
                config.Symbol, est.Nome, config.TerminalId);
            return;
        }

        var brokerSymbol = config.BrokerSymbol ?? config.Symbol;
        var posicoes = await gateway.ObterDetalhesPosicoesAbertasAsync(brokerSymbol, est.MagicNumber, ct);
        gruposConsultados.Add((config.TerminalId, brokerSymbol, est.MagicNumber));
        if (posicoes.Count == 0) return;

        foreach (var pos in posicoes)
            ticketsVistos.Add(pos.Ticket);

        // Fetch balance once per terminal
        if (!saldosPorTerminal.TryGetValue(config.TerminalId, out var saldo))
        {
            try
            {
                var info = await gateway.ObterInfoContaAsync(ct);
                saldo = info.Saldo;
                saldosPorTerminal[config.TerminalId] = saldo;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GestaoDinamica] Falha ao obter saldo da conta no terminal {TerminalId}. Saídas por % de conta serão puladas.", config.TerminalId);
                saldosPorTerminal[config.TerminalId] = null;
                saldo = null;
            }
        }

        var posicoesRemovidas = new HashSet<ulong>();

        // Prepara dados para saída técnica, se aplicável
        IReadOnlyList<CandleMt5>? candlesSaida = null;
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)>? indicadoresSaida = null;
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)>? indicadoresMulti = null;
        DateTime? tempoCandleFechado = null;

        if (estrategiaObj is IEstrategiaSaida)
        {
            var timeframeBase = est.Indicadores?.FirstOrDefault()?.Timeframe ?? "M1";
            var baseCandles = await gateway.ObterCandlesAsync(brokerSymbol, timeframeBase, 3, ct);
            tempoCandleFechado = baseCandles.LastOrDefault()?.Tempo;
        }

        // Precedência: Valor Bruto -> Percentual Conta -> [F2 Técnica] -> [Fixo antigo AcimaDe/AbaixoDe]
        foreach (var pos in posicoes)
        {
            if (saida != null && CalculoSaidaParametrica.AtingiuLimiteValorBruto(pos.LucroBruto, saida.TakeProfitValorBruto, saida.StopLossValorBruto, out string motivoBruto))
            {
                _logger.LogInformation("[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket}: {Motivo}", config.Symbol, est.Nome, pos.Ticket, motivoBruto);
                await _execucao.FecharPosicaoAsync(config.TerminalId, pos.Ticket, ct);
                posicoesRemovidas.Add(pos.Ticket);
                _ultimoCandleFechadoAvaliado.Remove(pos.Ticket);
                continue;
            }

            if (saldo.HasValue && saida != null && CalculoSaidaParametrica.AtingiuLimitePercentualConta(pos.LucroBruto, saldo.Value, saida.TakeProfitPercentualConta, saida.StopLossPercentualConta, out string motivoPct))
            {
                _logger.LogInformation("[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket}: {Motivo}", config.Symbol, est.Nome, pos.Ticket, motivoPct);
                await _execucao.FecharPosicaoAsync(config.TerminalId, pos.Ticket, ct);
                posicoesRemovidas.Add(pos.Ticket);
                _ultimoCandleFechadoAvaliado.Remove(pos.Ticket);
                continue;
            }

            if (estrategiaObj is IEstrategiaSaida saidaTecnica && tempoCandleFechado.HasValue)
            {
                _ultimoCandleFechadoAvaliado.TryGetValue(pos.Ticket, out var ultimoAvaliado);
                if (tempoCandleFechado.Value > ultimoAvaliado)
                {
                    if (candlesSaida == null)
                    {
                        int lookback = StrategyEngine.CalcularMaxPeriodoRequerido(est, estrategiaObj) + 10;
                        var timeframeBase = est.Indicadores?.FirstOrDefault()?.Timeframe ?? "M1";
                        candlesSaida = (await gateway.ObterCandlesAsync(brokerSymbol, timeframeBase, lookback, ct)).ToList();
                        
                        indicadoresSaida = await CalcularIndicadoresAsync(est, candlesSaida, gateway, brokerSymbol, ct);
                        indicadoresMulti = await _catalogoMultiFonte.CalcularTodosAsync(config.IndicadoresMultiFonte ?? [], _connectionManager.GetAllGateways(), ct);
                    }

                    var candleReferencia = candlesSaida[^1];
                    var tickReferencia = new TickEvent(
                        config.TerminalId,
                        brokerSymbol,
                        candleReferencia.Fechamento,
                        candleReferencia.Fechamento,
                        candleReferencia.Tempo);

                    var (fechar, motivoTecnico) = saidaTecnica.AvaliarSaida(pos.Compra, candlesSaida, indicadoresSaida!, indicadoresMulti!, pos, tickReferencia, est);
                    _ultimoCandleFechadoAvaliado[pos.Ticket] = tempoCandleFechado.Value;
                    
                    if (fechar)
                    {
                        _logger.LogInformation("[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket}: {Motivo}", config.Symbol, est.Nome, pos.Ticket, motivoTecnico);
                        await _execucao.FecharPosicaoAsync(config.TerminalId, pos.Ticket, ct);
                        posicoesRemovidas.Add(pos.Ticket);
                        _ultimoCandleFechadoAvaliado.Remove(pos.Ticket);
                        continue;
                    }
                }
            }

            if (saida != null && saida.EncerrarAtivaAcimaDe.HasValue && pos.PrecoAtual >= saida.EncerrarAtivaAcimaDe.Value)
            {
                _logger.LogInformation("[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket}: Saída por EncerrarAtivaAcimaDe ({Preco} >= {Limite})", config.Symbol, est.Nome, pos.Ticket, pos.PrecoAtual, saida.EncerrarAtivaAcimaDe.Value);
                await _execucao.FecharPosicaoAsync(config.TerminalId, pos.Ticket, ct);
                posicoesRemovidas.Add(pos.Ticket);
                _ultimoCandleFechadoAvaliado.Remove(pos.Ticket);
                continue;
            }

            if (saida != null && saida.EncerrarAtivaAbaixoDe.HasValue && pos.PrecoAtual <= saida.EncerrarAtivaAbaixoDe.Value)
            {
                _logger.LogInformation("[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket}: Saída por EncerrarAtivaAbaixoDe ({Preco} <= {Limite})", config.Symbol, est.Nome, pos.Ticket, pos.PrecoAtual, saida.EncerrarAtivaAbaixoDe.Value);
                await _execucao.FecharPosicaoAsync(config.TerminalId, pos.Ticket, ct);
                posicoesRemovidas.Add(pos.Ticket);
                _ultimoCandleFechadoAvaliado.Remove(pos.Ticket);
                continue;
            }
        }

        if (saida == null) return;

        var posicoesAtivas = posicoes.Where(p => !posicoesRemovidas.Contains(p.Ticket)).ToList();
        if (posicoesAtivas.Count == 0) return;

        var ponto = await gateway.ObterTamanhoPontoAsync(brokerSymbol, ct);

        double? atr = null;
        if (saida.BreakevenGatilhoAtrMultiplo is > 0 || saida.TrailingStopAtrMultiplo is > 0)
        {
            atr = await CalcularAtrAsync(gateway, brokerSymbol, ct);
            if (atr is null or <= 0)
            {
                _logger.LogWarning("[GestaoDinamica] {Simbolo}/{Estrategia}: ATR indisponível, pulando ciclo.",
                    config.Symbol, est.Nome);
                return;
            }
        }

        if (saida.SaidasParciais is { Count: > 0 } parciais)
        {
            var regrasVolume = await gateway.ObterRegrasVolumeAsync(brokerSymbol, ct);
            foreach (var pos in posicoesAtivas)
                await ProcessarParcialAsync(config, est, saida, parciais, pos, brokerSymbol, regrasVolume, ponto, ct);

            // Recarrega posições ativas apenas
            var recarregadas = await gateway.ObterDetalhesPosicoesAbertasAsync(brokerSymbol, est.MagicNumber, ct);
            posicoesAtivas = recarregadas.Where(p => !posicoesRemovidas.Contains(p.Ticket)).ToList();
        }

        foreach (var pos in posicoesAtivas)
        {
            var novoSl = CalcularNovoStopLoss(pos, saida, atr, ponto);
            if (novoSl is null) continue;

            var resultado = await _execucao.ModificarPosicaoAsync(
                config.TerminalId, pos.Ticket, novoSl.Value, pos.TakeProfitAtual, ct);

            if (resultado.Sucesso)
            {
                _logger.LogInformation(
                    "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} SL {Antigo:F5} -> {Novo:F5}",
                    config.Symbol, est.Nome, pos.Ticket, pos.StopLossAtual, novoSl.Value);
            }
            else
            {
                // Falha esperada e não-crítica se o preço estiver dentro do STOPS_LEVEL do símbolo —
                // tenta de novo no próximo ciclo, não é motivo para exceção.
                _logger.LogWarning(
                    "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} falha ao mover SL para {Novo:F5}: {Erro}",
                    config.Symbol, est.Nome, pos.Ticket, novoSl.Value, resultado.Motivo);
            }
        }
    }

    private async Task<IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)>> CalcularIndicadoresAsync(
        EstrategiaConfig config, IReadOnlyList<CandleMt5> candlesBase, IGatewayMt5 gateway, string brokerSymbol, CancellationToken ct)
    {
        var indicadores = config.Indicadores ?? [];
        if (indicadores.Count == 0) return [];

        var timeframeBase = indicadores.FirstOrDefault()?.Timeframe ?? "M1";
        var resultados = new List<(IndicadorConfig Config, ResultadoIndicador Resultado)>();

        foreach (var grupo in indicadores.GroupBy(i => i.Timeframe, StringComparer.OrdinalIgnoreCase))
        {
            var candles = grupo.Key.Equals(timeframeBase, StringComparison.OrdinalIgnoreCase)
                ? candlesBase
                : await ObterCandlesParaIndicadoresAsync(grupo, gateway, brokerSymbol, ct);

            resultados.AddRange(_catalogoIndicadores.CalcularTodos(grupo, candles));
        }

        return resultados;
    }

    private async Task<IReadOnlyList<CandleMt5>> ObterCandlesParaIndicadoresAsync(
        IEnumerable<IndicadorConfig> indicadores, IGatewayMt5 gateway, string brokerSymbol, CancellationToken ct)
    {
        var lista = indicadores.ToList();
        var timeframe = lista.First().Timeframe;
        var maxPeriodo = lista
            .Select(i => ParametroParser.ObterInt(i.Parametros, "periodo", 14))
            .DefaultIfEmpty(14)
            .Max();

        return await gateway.ObterCandlesAsync(brokerSymbol, timeframe, maxPeriodo + 60, ct);
    }

    /// <summary>
    /// Dispara a próxima saída parcial pendente para a posição, se o lucro atual (em preço)
    /// atingiu a distância configurada. Alvos são consumidos em ordem; no máximo uma parcial
    /// por posição por ciclo. Quando BreakEvenAposParcial está ativo, reposiciona o SL para
    /// a entrada (após a 1ª parcial) ou para a distância da parcial anterior (parciais seguintes).
    /// </summary>
    private async Task ProcessarParcialAsync(
        SymbolConfig config, EstrategiaConfig est, SaidaConfig saida,
        IReadOnlyList<SaidaParcialConfig> parciais, DetalhesPosicaoMt5 pos, string brokerSymbol,
        (double MinVolume, double MaxVolume, double VolumeStep) regrasVolume, double ponto, CancellationToken ct)
    {
        var proximoIndice = _estadoParciais.TryGetValue(pos.Ticket, out var estadoAtual) ? estadoAtual.ProximoIndice : 0;
        if (proximoIndice >= parciais.Count) return;

        var alvo = parciais[proximoIndice];
        var lucroAtual = CalculoSaidaParcial.CalcularLucroAtual(pos.Compra, pos.PrecoAbertura, pos.PrecoAtual);
        if (lucroAtual < (double)alvo.DistanciaPreco) return;

        var volumeFechar = CalculoSaidaParcial.CalcularVolumeParcial(pos.Volume, parciais, proximoIndice, regrasVolume);

        var resultado = await _execucao.FecharPosicaoParcialAsync(config.TerminalId, pos.Ticket, volumeFechar, ct);
        if (!resultado.Sucesso)
        {
            _logger.LogWarning(
                "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} falha ao executar parcial #{Indice} (Volume={Volume}): {Erro}",
                config.Symbol, est.Nome, pos.Ticket, proximoIndice + 1, volumeFechar, resultado.Motivo);
            return;
        }

        _logger.LogInformation(
            "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} Parcial #{Indice} executada: Volume={Volume} em lucro {Lucro:F2}.",
            config.Symbol, est.Nome, pos.Ticket, proximoIndice + 1, volumeFechar, lucroAtual);

        _estadoParciais[pos.Ticket] = new EstadoParcial(proximoIndice + 1, config.TerminalId, brokerSymbol, est.MagicNumber);

        if (!saida.BreakEvenAposParcial) return;

        var slBreakeven = CalculoSaidaParcial.CalcularBreakEvenAposParcial(pos.Compra, pos.PrecoAbertura, parciais, proximoIndice);
        var slNormalizado = NormalizarAoTick(slBreakeven, ponto);
        var melhora = pos.StopLossAtual <= 0 || (pos.Compra ? slNormalizado > pos.StopLossAtual : slNormalizado < pos.StopLossAtual);
        if (!melhora) return;

        var resultadoSl = await _execucao.ModificarPosicaoAsync(config.TerminalId, pos.Ticket, slNormalizado, pos.TakeProfitAtual, ct);
        if (resultadoSl.Sucesso)
        {
            _logger.LogInformation(
                "[GestaoDinamica] {Simbolo}/{Estrategia} Ticket={Ticket} Break-even pós-parcial: SL -> {Novo:F5}",
                config.Symbol, est.Nome, pos.Ticket, slNormalizado);
        }
    }

    /// <summary>
    /// Para tickets com progresso de parcial pendente cujo grupo (terminal/símbolo/magic) ainda não
    /// foi consultado neste ciclo, consulta o broker diretamente — mesmo que a estratégia dona do
    /// grupo não tenha breakeven/trailing/parciais ativos neste ciclo específico. Sem isso, um
    /// ticket real e aberto poderia ser podado só por não ter sido "visto", reiniciando o índice de
    /// parciais e disparando de novo uma saída parcial que já ocorreu no broker.
    /// </summary>
    private async Task ReconciliarTicketsPendentesAsync(
        HashSet<ulong> ticketsVistos, HashSet<(string TerminalId, string Simbolo, long MagicNumber)> gruposConsultados, CancellationToken ct)
    {
        var gruposPendentes = _estadoParciais.Values
            .Select(e => (e.TerminalId, e.Simbolo, e.MagicNumber))
            .Distinct()
            .Where(g => !gruposConsultados.Contains(g))
            .ToList();

        foreach (var grupo in gruposPendentes)
        {
            IGatewayMt5 gateway;
            try
            {
                gateway = _connectionManager.GetClient(grupo.TerminalId);
            }
            catch
            {
                // Terminal indisponível: não confirma nem poda — tenta reconciliar de novo no próximo ciclo.
                continue;
            }

            try
            {
                var posicoes = await gateway.ObterDetalhesPosicoesAbertasAsync(grupo.Simbolo, grupo.MagicNumber, ct);
                foreach (var pos in posicoes)
                    ticketsVistos.Add(pos.Ticket);

                gruposConsultados.Add(grupo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[GestaoDinamica] Falha ao reconciliar grupo {Simbolo}/Magic={Magic} para progresso de parciais pendente.",
                    grupo.Simbolo, grupo.MagicNumber);
            }
        }
    }

    private void PodarEstadoParciais(
        HashSet<ulong> ticketsVistos, HashSet<(string TerminalId, string Simbolo, long MagicNumber)> gruposConsultados)
    {
        // Só poda um ticket se o grupo a que ele pertence foi de fato consultado neste ciclo
        // (via processamento normal ou reconciliação) e ele não apareceu — confirmação real de
        // que a posição fechou, não apenas ausência de processamento.
        var ausentes = _estadoParciais
            .Where(kv => gruposConsultados.Contains((kv.Value.TerminalId, kv.Value.Simbolo, kv.Value.MagicNumber)) && !ticketsVistos.Contains(kv.Key))
            .Select(kv => kv.Key)
            .ToList();

        foreach (var ticket in ausentes)
            _estadoParciais.Remove(ticket);

        var candlesRemover = _ultimoCandleFechadoAvaliado.Keys
            .Where(k => !ticketsVistos.Contains(k))
            .ToList();

        foreach (var ticket in candlesRemover)
            _ultimoCandleFechadoAvaliado.Remove(ticket);
    }

    /// <summary>
    /// Calcula o novo SL (breakeven e/ou trailing), normaliza ao tick size, ou retorna null se nenhuma melhora se aplica.
    /// Nunca retorna um SL pior ou igual ao atual.
    /// </summary>
    private static double? CalcularNovoStopLoss(DetalhesPosicaoMt5 pos, SaidaConfig saida, double? atr, double ponto)
    {
        double? candidato = null;

        // --- Breakeven ---
        if (saida.BreakevenGatilhoAtrMultiplo is > 0 && atr is > 0)
        {
            var gatilho = (double)saida.BreakevenGatilhoAtrMultiplo!.Value * atr!.Value;
            var buffer = (double?)saida.BreakevenBufferPips ?? 0;
            var lucroAtual = pos.Compra ? pos.PrecoAtual - pos.PrecoAbertura : pos.PrecoAbertura - pos.PrecoAtual;

            if (lucroAtual >= gatilho)
            {
                var slBreakeven = pos.Compra ? pos.PrecoAbertura + buffer : pos.PrecoAbertura - buffer;
                candidato = MelhorSl(candidato, slBreakeven, pos.Compra);
            }
        }

        // --- Trailing stop ---
        // TrailingStopPips (distância fixa) tem prioridade sobre TrailingStopAtrMultiplo, que tem prioridade sobre TrailingStopPercentualPreco.
        // O ValidadorSaidaConfig já garante que só existe no máximo 1 fonte ativa por config,
        // mas a precedência aqui cobre o caso de fallback/segurança.
        double? distanciaTrailing = saida.TrailingStopPips is > 0
            ? (double)saida.TrailingStopPips!.Value
            : (saida.TrailingStopAtrMultiplo is > 0 && atr is > 0 ? (double)saida.TrailingStopAtrMultiplo!.Value * atr!.Value 
            : (saida.TrailingStopPercentualPreco is > 0 ? CalculoSaidaParametrica.CalcularDistanciaTrailingPercentual(pos.PrecoAtual, saida.TrailingStopPercentualPreco!.Value) : null));

        if (distanciaTrailing is > 0)
        {
            var slTrailing = pos.Compra ? pos.PrecoAtual - distanciaTrailing.Value : pos.PrecoAtual + distanciaTrailing.Value;
            candidato = MelhorSl(candidato, slTrailing, pos.Compra);
        }

        if (candidato is null) return null;

        var candidatoNormalizado = NormalizarAoTick(candidato.Value, ponto);

        // Nunca aplica um SL pior que o atual (SL=0 conta como "sem SL definido", qualquer candidato é melhora).
        var atualValido = pos.StopLossAtual > 0;
        if (atualValido)
        {
            var melhora = pos.Compra ? candidatoNormalizado > pos.StopLossAtual : candidatoNormalizado < pos.StopLossAtual;
            if (!melhora) return null;

            // Evitar spam de modificação para o mesmíssimo valor após normalização
            if (Math.Abs(candidatoNormalizado - pos.StopLossAtual) < (ponto / 2)) return null;
        }

        return candidatoNormalizado;
    }

    private static double? MelhorSl(double? atual, double novoCandidato, bool compra)
    {
        if (atual is null) return novoCandidato;
        return compra ? Math.Max(atual.Value, novoCandidato) : Math.Min(atual.Value, novoCandidato);
    }

    private static double NormalizarAoTick(double valor, double ponto)
    {
        if (ponto <= 0) return valor;
        return Math.Round(valor / ponto, MidpointRounding.AwayFromZero) * ponto;
    }

    /// <summary>ATR14 sobre o timeframe M1, calculado sob demanda.</summary>
    private static async Task<double?> CalcularAtrAsync(IGatewayMt5 gateway, string simbolo, CancellationToken ct)
    {
        var candles = await gateway.ObterCandlesAsync(simbolo, "M1", 15, ct);
        if (candles.Count < 15) return null;

        double somaTr = 0;
        for (int i = 1; i < candles.Count; i++)
        {
            var atual = candles[i];
            var anterior = candles[i - 1];
            var tr = Math.Max(atual.Maximo - atual.Minimo,
                     Math.Max(Math.Abs(atual.Maximo - anterior.Fechamento), Math.Abs(atual.Minimo - anterior.Fechamento)));
            somaTr += tr;
        }

        return somaTr / (candles.Count - 1);
    }

    private static EstrategiaConfig CriarEstrategiaFallback(SymbolConfig config) => new(
        Id: "cruzamento-ema", Nome: "CruzamentoEma", MagicNumber: 1, Ativa: true,
        Comprar: config.Comprar, Vender: config.Vender, Entrada: config.Entrada,
        Saida: config.Saida, GestaoDeRisco: config.GestaoDeRisco,
        JanelaHorarioPermitido: config.GestaoDeRisco?.JanelaHorarioPermitido,
        Indicadores: config.Indicadores, Parametros: new Dictionary<string, object>());
}
