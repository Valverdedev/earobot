using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.Interfaces;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Financial.Robot.Worker.MarketData;

public class MarketDataService : BackgroundService, IMarketDataService
{
    private readonly ILogger<MarketDataService> _logger;
    private readonly IConnectionManager _connectionManager;
    private readonly ConcurrentDictionary<Guid, Channel<TickEvent>> _assinantes = new();
    private readonly ConcurrentDictionary<string, DateTime> _ultimoLogPorSimbolo = new();

    public MarketDataService(ILogger<MarketDataService> logger, IConnectionManager connectionManager)
    {
        _logger = logger;
        _connectionManager = connectionManager;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(1000, stoppingToken);

        _logger.LogInformation("MarketDataService iniciado.");

        var terminals = _connectionManager.GetConnectedTerminalIds();
        foreach (var terminalId in terminals)
        {
            try
            {
                var gateway = _connectionManager.GetClient(terminalId);
                gateway.OnTick += (_, tickMt5) =>
                {
                    var tickEvent = new TickEvent(
                        terminalId,
                        tickMt5.Simbolo,
                        tickMt5.Bid,
                        tickMt5.Ask,
                        tickMt5.Tempo);

                    LogarFluxoDeTicks(terminalId, tickMt5);
                    PublicarTick(tickEvent);
                };

                _logger.LogInformation("MarketDataService inscrito nos ticks do terminal {TerminalId}.", terminalId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao se inscrever no terminal {TerminalId}.", terminalId);
            }
        }
    }

    public TickSubscription AssinarTicks()
    {
        var id = Guid.NewGuid();
        var canal = Channel.CreateUnbounded<TickEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });

        _assinantes[id] = canal;
        _logger.LogDebug("[MarketData] Nova assinatura de ticks registrada: {AssinaturaId}", id);

        return new TickSubscription(canal.Reader, () =>
        {
            if (_assinantes.TryRemove(id, out var removido))
            {
                removido.Writer.TryComplete();
                _logger.LogDebug("[MarketData] Assinatura de ticks removida: {AssinaturaId}", id);
            }
        });
    }

    private void PublicarTick(TickEvent tickEvent)
    {
        foreach (var (id, canal) in _assinantes)
        {
            if (!canal.Writer.TryWrite(tickEvent))
            {
                _assinantes.TryRemove(id, out _);
            }
        }
    }

    private void LogarFluxoDeTicks(string terminalId, TickMt5 tickMt5)
    {
        var key = $"{terminalId}-{tickMt5.Simbolo}";
        var agora = DateTime.UtcNow;

        _ultimoLogPorSimbolo.AddOrUpdate(
            key,
            _ =>
            {
                _logger.LogDebug(
                    "[MarketData] Fluxo de ticks ativo para {Symbol} (Terminal: {TerminalId}). Bid: {Bid} | Ask: {Ask}",
                    tickMt5.Simbolo, terminalId, tickMt5.Bid, tickMt5.Ask);

                return agora;
            },
            (_, ultimoLog) =>
            {
                if ((agora - ultimoLog).TotalSeconds < 10)
                    return ultimoLog;

                _logger.LogDebug(
                    "[MarketData] Fluxo de ticks ativo para {Symbol} (Terminal: {TerminalId}). Bid: {Bid} | Ask: {Ask}",
                    tickMt5.Simbolo, terminalId, tickMt5.Bid, tickMt5.Ask);

                return agora;
            });
    }
}
