using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Infrastructure.Gateways;

/// <summary>
/// Decorator para IGatewayMt5 que bloqueia operações de escrita (envio de ordens).
/// Repassa todas as chamadas de leitura para o gateway subjacente e simula sucesso nas escritas.
/// </summary>
public sealed class GatewayMt5SomenteLeitura : IGatewayMt5, IAsyncDisposable
{
    private readonly IGatewayMt5 _inner;
    private readonly ILogger _logger;

    public GatewayMt5SomenteLeitura(IGatewayMt5 inner, ILogger logger)
    {
        _inner = inner;
        _logger = logger;
        _inner.OnTick += (sender, tick) => OnTick?.Invoke(this, tick);
    }

    public event EventHandler<TickMt5>? OnTick;

    public bool EstaConectado => _inner.EstaConectado;

    public Task ConectarAsync(string host, int porta, int timeoutSegundos, CancellationToken ct = default)
        => _inner.ConectarAsync(host, porta, timeoutSegundos, ct);

    public Task DesconectarAsync(CancellationToken ct = default)
        => _inner.DesconectarAsync(ct);

    public Task<InfoContaMt5> ObterInfoContaAsync(CancellationToken ct = default)
        => _inner.ObterInfoContaAsync(ct);

    public Task<TickMt5> ObterTickAtualAsync(string simbolo, CancellationToken ct = default)
        => _inner.ObterTickAtualAsync(simbolo, ct);

    public Task<double> ObterTamanhoPontoAsync(string simbolo, CancellationToken ct = default)
        => _inner.ObterTamanhoPontoAsync(simbolo, ct);

    public Task<double> ObterStopsLevelAsync(string simbolo, CancellationToken ct = default)
        => _inner.ObterStopsLevelAsync(simbolo, ct);

    public Task<(double MinVolume, double MaxVolume, double VolumeStep)> ObterRegrasVolumeAsync(string simbolo, CancellationToken ct = default)
        => _inner.ObterRegrasVolumeAsync(simbolo, ct);

    public Task<IReadOnlyList<CandleMt5>> ObterCandlesAsync(string simbolo, string timeframe, int quantidade, CancellationToken ct = default)
        => _inner.ObterCandlesAsync(simbolo, timeframe, quantidade, ct);

    public Task<bool> SubscreverSimboloAsync(string simbolo, CancellationToken ct = default)
        => _inner.SubscreverSimboloAsync(simbolo, ct);

    public Task<IReadOnlyList<ulong>> ObterTicketsPosicoesAbertasAsync(string simbolo, long? magicNumber = null, CancellationToken ct = default)
        => _inner.ObterTicketsPosicoesAbertasAsync(simbolo, magicNumber, ct);

    public Task<int> ObterStopsConsecutivosAsync(string simbolo, long magicNumber, CancellationToken ct = default)
        => _inner.ObterStopsConsecutivosAsync(simbolo, magicNumber, ct);

    public Task<IReadOnlyList<DetalhesPosicaoMt5>> ObterDetalhesPosicoesAbertasAsync(string simbolo, long? magicNumber = null, CancellationToken ct = default)
        => _inner.ObterDetalhesPosicoesAbertasAsync(simbolo, magicNumber, ct);

    public Task<double> ObterLucroPrejuizoDiaAsync(string simbolo, long magicNumber, DateTime inicioDia, CancellationToken ct = default)
        => _inner.ObterLucroPrejuizoDiaAsync(simbolo, magicNumber, inicioDia, ct);

    public Task<double> ObterLucroAbertoAsync(string simbolo, long magicNumber, CancellationToken ct = default)
        => _inner.ObterLucroAbertoAsync(simbolo, magicNumber, ct);

    public Task<DateTime?> ObterMomentoUltimoFechamentoAsync(string simbolo, long magicNumber, CancellationToken ct = default)
        => _inner.ObterMomentoUltimoFechamentoAsync(simbolo, magicNumber, ct);

    public Task<IReadOnlyList<DealMt5>> ObterDealsHistoricosAsync(
        string simbolo,
        long? magicNumber,
        DateTime inicioUtc,
        DateTime fimUtc,
        CancellationToken ct = default)
        => _inner.ObterDealsHistoricosAsync(simbolo, magicNumber, inicioUtc, fimUtc, ct);

    // --- BLOQUEIOS DE ESCRITA ---

    public Task<ResultadoOrdemMt5> AbrirOrdemMercadoAsync(
        string simbolo, string tipoOrdem, double volume, double stopLoss, double takeProfit, string comentario, long magicNumber, CancellationToken ct = default)
    {
        _logger.LogInformation("[SOMENTE LEITURA] Bloqueada Ordem a Mercado: {TipoOrdem} {Volume} em {Simbolo} (SL: {SL}, TP: {TP}, Magic: {Magic})", 
            tipoOrdem, volume, simbolo, stopLoss, takeProfit, magicNumber);
        
        // Simula sucesso sem enviar ordem ao MT5
        return Task.FromResult(new ResultadoOrdemMt5(true, 0, 0, volume, 0, "[SOMENTE LEITURA]"));
    }

    public Task<bool> ModificarPosicaoAsync(ulong ticket, double novoStopLoss, double novoTakeProfit, CancellationToken ct = default)
    {
        _logger.LogInformation("[SOMENTE LEITURA] Bloqueada Modificação de Posição {Ticket}: SL={SL}, TP={TP}", ticket, novoStopLoss, novoTakeProfit);
        return Task.FromResult(true);
    }

    public Task<ResultadoFechamentoMt5> FecharPosicaoAsync(ulong ticket, CancellationToken ct = default)
    {
        _logger.LogInformation("[SOMENTE LEITURA] Bloqueado Fechamento da Posição {Ticket}", ticket);
        return Task.FromResult(new ResultadoFechamentoMt5(true, 0, 0, 0, "[SOMENTE LEITURA]"));
    }

    public Task<ResultadoFechamentoMt5> FecharPosicaoParcialAsync(ulong ticket, double volume, CancellationToken ct = default)
    {
        _logger.LogInformation("[SOMENTE LEITURA] Bloqueado Fechamento Parcial da Posição {Ticket}: Volume={Volume}", ticket, volume);
        return Task.FromResult(new ResultadoFechamentoMt5(true, 0, 0, 0, "[SOMENTE LEITURA]"));
    }

    public Task<ResultadoOrdemMt5> CriarOrdemPendenteAsync(
        string simbolo, string tipoOrdem, double volume, double precoOrdem, double stopLoss, double takeProfit, long magicNumber, CancellationToken ct = default)
    {
        _logger.LogInformation("[SOMENTE LEITURA] Bloqueada Ordem Pendente: {TipoOrdem} {Volume} em {Simbolo} a {PrecoOrdem} (SL: {SL}, TP: {TP}, Magic: {Magic})", 
            tipoOrdem, volume, simbolo, precoOrdem, stopLoss, takeProfit, magicNumber);
        
        return Task.FromResult(new ResultadoOrdemMt5(true, 0, 0, volume, 0, "[SOMENTE LEITURA]"));
    }

    public Task<bool> CancelarOrdemPendenteAsync(ulong ticket, CancellationToken ct = default)
    {
        _logger.LogInformation("[SOMENTE LEITURA] Bloqueado Cancelamento da Ordem Pendente {Ticket}", ticket);
        return Task.FromResult(true);
    }

    public async ValueTask DisposeAsync()
    {
        if (_inner is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync();
        }
    }
}
