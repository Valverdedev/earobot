using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using MtApi5;
using System.Diagnostics;

namespace Financial.Robot.Infrastructure.Gateways;

/// <summary>
/// Implementação do Gateway MT5 usando a biblioteca MtApi5 (vdemydiuk/mtapi).
/// </summary>
public sealed class GatewayMt5 : IGatewayMt5, IAsyncDisposable
{
    private readonly ILogger<GatewayMt5> _logger;
    private readonly MtApi5Client _client;

    private string? _host;
    private int _porta;
    private int _timeoutSegundos;
    private bool _desconectadoIntencionalmente;
    private bool _reconnecting;

    /// <inheritdoc/>
    public event EventHandler<TickMt5>? OnTick;

    /// <inheritdoc/>
    public bool EstaConectado => _client.ConnectionState == Mt5ConnectionState.Connected;

    /// <summary>Inicializa o gateway com logger.</summary>
    public GatewayMt5(ILogger<GatewayMt5> logger)
    {
        _logger = logger;
        _client = new MtApi5Client();
        _client.ConnectionStateChanged += (s, e) =>
        {
            _logger.LogInformation("MT5 Connection State: {Status} (Msg: {Msg})", e.Status, e.ConnectionMessage);
            if ((e.Status == Mt5ConnectionState.Disconnected || e.Status == Mt5ConnectionState.Failed) &&
                !_desconectadoIntencionalmente && !_reconnecting)
            {
                _ = ReconnectAsync();
            }
        };
        _client.QuoteUpdate += (s, e) =>
        {
            if (e.Quote != null)
            {
                OnTick?.Invoke(this, new TickMt5(e.Quote.Instrument, e.Quote.Bid, e.Quote.Ask, e.Quote.Time));
            }
        };
    }

    public async Task ConectarAsync(string host, int porta, int timeoutSegundos, CancellationToken ct = default)
    {
        _host = host;
        _porta = porta;
        _timeoutSegundos = timeoutSegundos;
        _desconectadoIntencionalmente = false;

        try
        {
            await _client.Connect(host, porta);

            var stopWatch = Stopwatch.StartNew();
            while (_client.ConnectionState != Mt5ConnectionState.Connected)
            {
                if (stopWatch.Elapsed.TotalSeconds > timeoutSegundos)
                {
                    throw new TimeoutException($"Timeout ao conectar no MT5 após {timeoutSegundos}s.");
                }

                if (_client.ConnectionState == Mt5ConnectionState.Disconnected ||
                    _client.ConnectionState == Mt5ConnectionState.Failed)
                {
                    throw new ConexaoMt5Exception("Falha na conexão com MT5. Estado: " + _client.ConnectionState);
                }

                await Task.Delay(500, ct);
            }
        }
        catch (Exception ex)
        {
            throw new ConexaoMt5Exception($"Falha ao conectar no MT5: {host}:{porta}", ex);
        }
    }

    /// <inheritdoc/>
    public Task DesconectarAsync(CancellationToken ct = default)
    {
        _desconectadoIntencionalmente = true;
        _client.Disconnect();
        return Task.CompletedTask;
    }

    private async Task ReconnectAsync()
    {
        if (_host == null) return;
        _reconnecting = true;
        _logger.LogWarning("Tentando auto-reconexão com MT5 em {Host}:{Porta}...", _host, _porta);

        while (!_desconectadoIntencionalmente && _client.ConnectionState != Mt5ConnectionState.Connected)
        {
            try
            {
                await Task.Delay(5000); // Backoff de 5 segundos
                if (_desconectadoIntencionalmente) break;

                await _client.Connect(_host, _porta);

                // Aguarda brevemente para ver se conectou
                for (int i = 0; i < 10; i++)
                {
                    if (_client.ConnectionState == Mt5ConnectionState.Connected) break;
                    await Task.Delay(500);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Auto-reconexão falhou: {Msg}. Tentando novamente em breve...", ex.Message);
            }
        }

        if (_client.ConnectionState == Mt5ConnectionState.Connected)
        {
            _logger.LogInformation("Auto-reconexão com MT5 bem-sucedida!");
        }
        _reconnecting = false;
    }

    /// <inheritdoc/>
    public Task<InfoContaMt5> ObterInfoContaAsync(CancellationToken ct = default)
    {
        ValidarConexao();

        var login = _client.AccountInfoInteger(ENUM_ACCOUNT_INFO_INTEGER.ACCOUNT_LOGIN);
        var servidor = _client.AccountInfoString(ENUM_ACCOUNT_INFO_STRING.ACCOUNT_SERVER) ?? "Desconhecido";
        var nomeBruto = _client.AccountInfoString(ENUM_ACCOUNT_INFO_STRING.ACCOUNT_NAME);
        var nome = string.IsNullOrWhiteSpace(nomeBruto) || nomeBruto.Contains("null")
            ? (_client.AccountInfoString(ENUM_ACCOUNT_INFO_STRING.ACCOUNT_COMPANY) ?? "Conta Desconhecida")
            : nomeBruto;
        var saldo = _client.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_BALANCE);
        var equidade = _client.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_EQUITY);
        var margemLivre = _client.AccountInfoDouble(ENUM_ACCOUNT_INFO_DOUBLE.ACCOUNT_MARGIN_FREE);
        var moeda = _client.AccountInfoString(ENUM_ACCOUNT_INFO_STRING.ACCOUNT_CURRENCY) ?? "USD";

        return Task.FromResult(new InfoContaMt5(login, servidor, nome, saldo, equidade, margemLivre, moeda));
    }

    /// <inheritdoc/>
    public Task<TickMt5> ObterTickAtualAsync(string simbolo, CancellationToken ct = default)
    {
        ValidarConexao();

        if (!_client.SymbolInfoTick(simbolo, out var tick) || tick == null)
            throw new DomainException($"Não foi possível obter tick para {simbolo}.");

        return Task.FromResult(new TickMt5(simbolo, tick.bid, tick.ask, tick.time));
    }

    /// <inheritdoc/>
    public Task<double> ObterTamanhoPontoAsync(string simbolo, CancellationToken ct = default)
    {
        ValidarConexao();
        var tickSize = _client.SymbolInfoDouble(simbolo, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_TRADE_TICK_SIZE);
        if (tickSize <= 0)
            tickSize = _client.SymbolInfoDouble(simbolo, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_POINT);
        return Task.FromResult(tickSize);
    }

    /// <inheritdoc/>
    public Task<double> ObterStopsLevelAsync(string simbolo, CancellationToken ct = default)
    {
        ValidarConexao();
        var level = _client.SymbolInfoInteger(simbolo, ENUM_SYMBOL_INFO_INTEGER.SYMBOL_TRADE_STOPS_LEVEL);
        return Task.FromResult((double)level);
    }

    /// <inheritdoc/>
    public Task<(double MinVolume, double MaxVolume, double VolumeStep)> ObterRegrasVolumeAsync(string simbolo, CancellationToken ct = default)
    {
        ValidarConexao();
        var min = _client.SymbolInfoDouble(simbolo, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_VOLUME_MIN);
        var max = _client.SymbolInfoDouble(simbolo, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_VOLUME_MAX);
        var step = _client.SymbolInfoDouble(simbolo, ENUM_SYMBOL_INFO_DOUBLE.SYMBOL_VOLUME_STEP);
        return Task.FromResult((min, max, step));
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<CandleMt5>> ObterCandlesAsync(string simbolo, string timeframe, int quantidade, CancellationToken ct = default)
    {
        ValidarConexao();

        var tf = ConverterTimeframe(timeframe);
        _client.CopyRates(simbolo, tf, 0, quantidade, out MqlRates[]? rates);

        if (rates == null || rates.Length == 0)
            return Task.FromResult<IReadOnlyList<CandleMt5>>(Array.Empty<CandleMt5>());

        var candles = rates
            .Select(r => new CandleMt5(r.time, r.open, r.high, r.low, r.close, r.tick_volume))
            .ToList();

        return Task.FromResult<IReadOnlyList<CandleMt5>>(candles);
    }

    /// <inheritdoc/>
    public Task<bool> SubscreverSimboloAsync(string simbolo, CancellationToken ct = default)
    {
        ValidarConexao();
        // SymbolSelect adiciona o símbolo ao Market Watch, habilitando o recebimento de QuoteUpdate
        var ok = _client.SymbolSelect(simbolo, true);
        _logger.LogInformation("[Gateway] Subscrevendo símbolo {Symbol} no Market Watch. Resultado: {Ok}", simbolo, ok);
        return Task.FromResult(ok);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<ulong>> ObterTicketsPosicoesAbertasAsync(string simbolo, long? magicNumber = null, CancellationToken ct = default)
    {
        ValidarConexao();
        var tickets = new List<ulong>();
        var total = _client.PositionsTotal();

        for (int i = 0; i < total; i++)
        {
            var ticket = _client.PositionGetTicket(i);
            var sym = _client.PositionGetString(ENUM_POSITION_PROPERTY_STRING.POSITION_SYMBOL);
            if (sym == simbolo)
            {
                if (magicNumber.HasValue)
                {
                    var m = _client.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_MAGIC);
                    if (m == magicNumber.Value) tickets.Add(ticket);
                }
                else
                {
                    tickets.Add(ticket);
                }
            }
        }

        return Task.FromResult<IReadOnlyList<ulong>>(tickets);
    }

    public Task<int> ObterStopsConsecutivosAsync(string simbolo, long magicNumber, CancellationToken ct = default)
    {
        ValidarConexao();

        // Considera apenas a sequencia intraday para nao carregar stops de dias anteriores.
        if (!_client.HistorySelect(DateTime.UtcNow.Date, DateTime.UtcNow))
            return Task.FromResult(0);

        int count = 0;
        int total = _client.HistoryDealsTotal();

        for (int i = total - 1; i >= 0; i--)
        {
            var ticket = _client.HistoryDealGetTicket(i);
            if (ticket > 0)
            {
                var sym = _client.HistoryDealGetString(ticket, ENUM_DEAL_PROPERTY_STRING.DEAL_SYMBOL);
                var magic = _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_MAGIC);
                var entry = (ENUM_DEAL_ENTRY)_client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_ENTRY);

                if (sym == simbolo && magic == magicNumber &&
                    (entry == ENUM_DEAL_ENTRY.DEAL_ENTRY_OUT || entry == ENUM_DEAL_ENTRY.DEAL_ENTRY_INOUT))
                {
                    var profit = _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_PROFIT);
                    if (profit < 0)
                    {
                        count++;
                    }
                    else if (profit > 0)
                    {
                        break; // Ao encontrar o primeiro gain (voltando no tempo), quebra a sequência
                    }
                }
            }
        }

        return Task.FromResult(count);
    }

    public Task<double> ObterLucroPrejuizoDiaAsync(string simbolo, long magicNumber, DateTime inicioDia, CancellationToken ct = default)
    {
        ValidarConexao();

        if (!_client.HistorySelect(inicioDia, DateTime.UtcNow.AddDays(1)))
            return Task.FromResult(0.0);

        double lucroDia = 0.0;
        int total = _client.HistoryDealsTotal();
        for (int i = 0; i < total; i++)
        {
            var ticket = _client.HistoryDealGetTicket(i);
            if (ticket > 0)
            {
                var sym = _client.HistoryDealGetString(ticket, ENUM_DEAL_PROPERTY_STRING.DEAL_SYMBOL);
                var magic = _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_MAGIC);

                if (sym == simbolo && magic == magicNumber)
                {
                    lucroDia += _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_PROFIT);
                    lucroDia += _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_SWAP);
                    lucroDia += _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_COMMISSION);
                }
            }
        }

        return Task.FromResult(lucroDia);
    }

    public Task<double> ObterLucroAbertoAsync(string simbolo, long magicNumber, CancellationToken ct = default)
    {
        ValidarConexao();

        var total = _client.PositionsTotal();
        double lucro = 0.0;
        for (int i = 0; i < total; i++)
        {
            var sym = _client.PositionGetSymbol(i);
            if (sym == simbolo)
            {
                var magic = _client.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_MAGIC);
                if (magic == magicNumber)
                {
                    lucro += _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PROFIT);
                }
            }
        }

        return Task.FromResult(lucro);
    }

    public Task<DateTime?> ObterMomentoUltimoFechamentoAsync(string simbolo, long magicNumber, CancellationToken ct = default)
    {
        ValidarConexao();

        if (!_client.HistorySelect(DateTime.UtcNow.AddDays(-1), DateTime.UtcNow.AddDays(1)))
            return Task.FromResult<DateTime?>(null);

        DateTime? ultimoTempo = null;
        int total = _client.HistoryDealsTotal();
        for (int i = 0; i < total; i++)
        {
            var ticket = _client.HistoryDealGetTicket(i);
            if (ticket > 0)
            {
                var sym = _client.HistoryDealGetString(ticket, ENUM_DEAL_PROPERTY_STRING.DEAL_SYMBOL);
                var magic = _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_MAGIC);
                var entry = (ENUM_DEAL_ENTRY)_client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_ENTRY);

                if (sym == simbolo && magic == magicNumber &&
                    (entry == ENUM_DEAL_ENTRY.DEAL_ENTRY_OUT || entry == ENUM_DEAL_ENTRY.DEAL_ENTRY_INOUT))
                {
                    var timeSeconds = _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TIME);
                    var date = DateTimeOffset.FromUnixTimeSeconds(timeSeconds).UtcDateTime;

                    if (ultimoTempo == null || date > ultimoTempo)
                    {
                        ultimoTempo = date;
                    }
                }
            }
        }

        return Task.FromResult(ultimoTempo);
    }

    public Task<IReadOnlyList<DealMt5>> ObterDealsHistoricosAsync(
        string simbolo,
        long? magicNumber,
        DateTime inicioUtc,
        DateTime fimUtc,
        CancellationToken ct = default)
    {
        ValidarConexao();

        if (!_client.HistorySelect(inicioUtc, fimUtc))
            return Task.FromResult<IReadOnlyList<DealMt5>>(Array.Empty<DealMt5>());

        var deals = new List<DealMt5>();
        var total = _client.HistoryDealsTotal();

        for (var i = 0; i < total; i++)
        {
            var ticket = _client.HistoryDealGetTicket(i);
            if (ticket == 0) continue;

            var sym = _client.HistoryDealGetString(ticket, ENUM_DEAL_PROPERTY_STRING.DEAL_SYMBOL);
            if (sym != simbolo) continue;

            var magic = _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_MAGIC);
            if (magicNumber.HasValue && magic != magicNumber.Value) continue;

            var timeSeconds = _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TIME);
            var timeUtc = DateTimeOffset.FromUnixTimeSeconds(timeSeconds).UtcDateTime;

            deals.Add(new DealMt5(
                Ticket: ticket,
                Order: (ulong)_client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_ORDER),
                PositionId: _client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_POSITION_ID),
                Simbolo: sym,
                MagicNumber: magic,
                TimeUtc: timeUtc,
                Type: ((ENUM_DEAL_TYPE)_client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_TYPE)).ToString(),
                Entry: ((ENUM_DEAL_ENTRY)_client.HistoryDealGetInteger(ticket, ENUM_DEAL_PROPERTY_INTEGER.DEAL_ENTRY)).ToString(),
                Price: _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_PRICE),
                Volume: _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_VOLUME),
                Profit: _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_PROFIT),
                Commission: _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_COMMISSION),
                Swap: _client.HistoryDealGetDouble(ticket, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_SWAP),
                Comment: _client.HistoryDealGetString(ticket, ENUM_DEAL_PROPERTY_STRING.DEAL_COMMENT)));
        }

        return Task.FromResult<IReadOnlyList<DealMt5>>(deals.OrderBy(d => d.TimeUtc).ToList());
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DetalhesPosicaoMt5>> ObterDetalhesPosicoesAbertasAsync(
        string simbolo, long? magicNumber = null, CancellationToken ct = default)
    {
        ValidarConexao();
        var resultado = new List<DetalhesPosicaoMt5>();
        var total = _client.PositionsTotal();

        for (int i = 0; i < total; i++)
        {
            var ticket = _client.PositionGetTicket(i);
            var sym = _client.PositionGetString(ENUM_POSITION_PROPERTY_STRING.POSITION_SYMBOL);
            if (sym != simbolo) continue;

            var magic = _client.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_MAGIC);
            if (magicNumber.HasValue && magic != magicNumber.Value) continue;

            var tipo = _client.PositionGetInteger(ENUM_POSITION_PROPERTY_INTEGER.POSITION_TYPE);
            var compra = tipo == (long)ENUM_POSITION_TYPE.POSITION_TYPE_BUY;

            resultado.Add(new DetalhesPosicaoMt5(
                Ticket: ticket,
                Simbolo: sym,
                MagicNumber: magic,
                Compra: compra,
                PrecoAbertura: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PRICE_OPEN),
                PrecoAtual: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PRICE_CURRENT),
                StopLossAtual: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_SL),
                TakeProfitAtual: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_TP),
                Volume: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_VOLUME),
                LucroBruto: _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PROFIT)));
        }

        return Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(resultado);
    }

    private static ENUM_TIMEFRAMES ConverterTimeframe(string timeframe) => timeframe.ToUpperInvariant() switch
    {
        "M1" => ENUM_TIMEFRAMES.PERIOD_M1,
        "M5" => ENUM_TIMEFRAMES.PERIOD_M5,
        "M15" => ENUM_TIMEFRAMES.PERIOD_M15,
        "M30" => ENUM_TIMEFRAMES.PERIOD_M30,
        "H1" => ENUM_TIMEFRAMES.PERIOD_H1,
        "H4" => ENUM_TIMEFRAMES.PERIOD_H4,
        "D1" => ENUM_TIMEFRAMES.PERIOD_D1,
        "W1" => ENUM_TIMEFRAMES.PERIOD_W1,
        "MN1" => ENUM_TIMEFRAMES.PERIOD_MN1,
        _ => throw new ArgumentException($"Timeframe desconhecido: {timeframe}")
    };

    /// <inheritdoc/>
    public Task<ResultadoOrdemMt5> AbrirOrdemMercadoAsync(
        string simbolo, string tipoOrdem, double volume,
        double stopLoss, double takeProfit, string comentario,
        long magicNumber,
        CancellationToken ct = default)
    {
        ValidarConexao();

        var orderType = tipoOrdem.Equals("BUY", StringComparison.OrdinalIgnoreCase)
            ? ENUM_ORDER_TYPE.ORDER_TYPE_BUY
            : ENUM_ORDER_TYPE.ORDER_TYPE_SELL;

        var request = new MqlTradeRequest
        {
            Action = ENUM_TRADE_REQUEST_ACTIONS.TRADE_ACTION_DEAL,
            Symbol = simbolo,
            Volume = volume,
            Sl = stopLoss,
            Tp = takeProfit,
            Type = orderType,
            Comment = comentario,
            Magic = (ulong)magicNumber
        };

        var sucesso = _client.OrderSend(request, out var result);

        if (result == null)
            throw new ConexaoMt5Exception("O MT5 não retornou resultado para a ordem.");

        return Task.FromResult(new ResultadoOrdemMt5(
            sucesso,
            result.Order,
            result.Price,
            result.Volume,
            (int)result.Retcode,
            result.Comment));
    }

    /// <inheritdoc/>
    public Task<bool> ModificarPosicaoAsync(
        ulong ticket, double novoStopLoss, double novoTakeProfit,
        CancellationToken ct = default)
    {
        ValidarConexao();
        var sucesso = _client.PositionModify(ticket, novoStopLoss, novoTakeProfit);

        if (sucesso)
        {
            if (_client.PositionSelectByTicket(ticket))
            {
                var sl = _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_SL);
                var tp = _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_TP);
                _logger.LogInformation("MT5 Ticket {Ticket} confirmado: SL = {SL}, TP = {TP}", ticket, sl, tp);
            }
        }

        return Task.FromResult(sucesso);
    }

    /// <inheritdoc/>
    public Task<ResultadoFechamentoMt5> FecharPosicaoAsync(
        ulong ticket, CancellationToken ct = default)
    {
        ValidarConexao();

        var sucesso = _client.PositionClose(ticket, out var result);

        if (result == null)
            throw new ConexaoMt5Exception("O MT5 não retornou resultado ao fechar a posição.");

        double lucro = 0.0;
        if (sucesso && result.Deal > 0)
        {
            // O histórico precisa de um breve instante para o terminal registrar a deal no MQL5 backend
            // Em testes reais de HFT, o HistoryDealSelect pode falhar se não houver um delay minúsculo, mas aqui o proxy lida bem
            if (_client.HistoryDealSelect(result.Deal))
            {
                lucro = _client.HistoryDealGetDouble(result.Deal, ENUM_DEAL_PROPERTY_DOUBLE.DEAL_PROFIT);
            }
        }

        return Task.FromResult(new ResultadoFechamentoMt5(
            sucesso,
            result.Price,
            lucro,
            (int)result.Retcode,
            result.Comment));
    }

    /// <inheritdoc/>
    public Task<ResultadoFechamentoMt5> FecharPosicaoParcialAsync(
        ulong ticket, double volume, CancellationToken ct = default)
    {
        ValidarConexao();

        var sucesso = _client.PositionClosePartial(ticket, volume);

        double precoAtual = 0;
        if (sucesso && _client.PositionSelectByTicket(ticket))
        {
            precoAtual = _client.PositionGetDouble(ENUM_POSITION_PROPERTY_DOUBLE.POSITION_PRICE_CURRENT);
        }

        return Task.FromResult(new ResultadoFechamentoMt5(
            sucesso,
            precoAtual,
            0,
            0,
            sucesso ? null : "PositionClosePartial retornou false"));
    }

    /// <inheritdoc/>
    public Task<ResultadoOrdemMt5> CriarOrdemPendenteAsync(
        string simbolo, string tipoOrdem, double volume,
        double precoOrdem, double stopLoss, double takeProfit,
        long magicNumber,
        CancellationToken ct = default)
    {
        ValidarConexao();

        ENUM_ORDER_TYPE orderType = tipoOrdem.ToUpperInvariant() switch
        {
            "BUY_LIMIT" => ENUM_ORDER_TYPE.ORDER_TYPE_BUY_LIMIT,
            "SELL_LIMIT" => ENUM_ORDER_TYPE.ORDER_TYPE_SELL_LIMIT,
            "BUY_STOP" => ENUM_ORDER_TYPE.ORDER_TYPE_BUY_STOP,
            "SELL_STOP" => ENUM_ORDER_TYPE.ORDER_TYPE_SELL_STOP,
            _ => throw new DomainException($"Tipo de ordem pendente não suportado: {tipoOrdem}")
        };

        var request = new MqlTradeRequest
        {
            Action = ENUM_TRADE_REQUEST_ACTIONS.TRADE_ACTION_PENDING,
            Symbol = simbolo,
            Volume = volume,
            Price = precoOrdem,
            Sl = stopLoss,
            Tp = takeProfit,
            Type = orderType,
            Comment = "financial.robot pendente",
            Magic = (ulong)magicNumber
        };

        var sucesso = _client.OrderSend(request, out var result);

        if (result == null)
            throw new ConexaoMt5Exception("O MT5 não retornou resultado para a ordem pendente.");

        return Task.FromResult(new ResultadoOrdemMt5(
            sucesso,
            result.Order,
            result.Price,
            result.Volume,
            (int)result.Retcode,
            result.Comment));
    }

    /// <inheritdoc/>
    public Task<bool> CancelarOrdemPendenteAsync(ulong ticket, CancellationToken ct = default)
    {
        ValidarConexao();

        var request = new MqlTradeRequest
        {
            Action = ENUM_TRADE_REQUEST_ACTIONS.TRADE_ACTION_REMOVE,
            Order = ticket
        };

        var sucesso = _client.OrderSend(request, out _);
        return Task.FromResult(sucesso);
    }

    private void ValidarConexao()
    {
        if (!EstaConectado)
            throw new ConexaoMt5Exception("Gateway não está conectado ao MT5.");
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        _client.Disconnect();
        return ValueTask.CompletedTask;
    }
}
