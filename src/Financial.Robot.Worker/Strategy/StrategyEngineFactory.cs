using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Infrastructure.Mt5;
using Financial.Robot.Worker.Config;
using Financial.Robot.Worker.Execution;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Risk;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Strategy;

/// <summary>
/// Cria, mantém e destrói uma StrategyEngine por símbolo ativo.
/// Reage ao evento ConfigChanged do ConfigWatcherService.
/// </summary>
public sealed class StrategyEngineFactory : BackgroundService
{
    private readonly ConfigWatcherService _configWatcher;
    private readonly ConnectionManagerService _connectionManager;
    private readonly IMarketDataService _marketData;
    private readonly CatalogoIndicadores _catalogo;
    private readonly CatalogoIndicadoresMultiFonte _catalogoMultiFonte;
    private readonly CatalogoEstrategias _catalogoEstrategias;
    private readonly RiskGuard _riskGuard;
    private readonly CalculadoraLote _calculadoraLote;
    private readonly IServicoExecucao _execucao;
    private readonly ILogger<StrategyEngine> _engineLogger;
    private readonly ILogger<StrategyEngineFactory> _logger;

    private readonly Dictionary<string, List<StrategyEngine>> _enginesPorSimbolo = new();
    private readonly HashSet<string> _enginesPendentes = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public StrategyEngineFactory(
        ConfigWatcherService configWatcher,
        ConnectionManagerService connectionManager,
        IMarketDataService marketData,
        CatalogoIndicadores catalogo,
        CatalogoIndicadoresMultiFonte catalogoMultiFonte,
        CatalogoEstrategias catalogoEstrategias,
        RiskGuard riskGuard,
        CalculadoraLote calculadoraLote,
        IServicoExecucao execucao,
        ILogger<StrategyEngine> engineLogger,
        ILogger<StrategyEngineFactory> logger)
    {
        _configWatcher = configWatcher;
        _connectionManager = connectionManager;
        _marketData = marketData;
        _catalogo = catalogo;
        _catalogoMultiFonte = catalogoMultiFonte;
        _catalogoEstrategias = catalogoEstrategias;
        _riskGuard = riskGuard;
        _calculadoraLote = calculadoraLote;
        _execucao = execucao;
        _engineLogger = engineLogger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Subscreve ao evento de mudança de config
        _configWatcher.ConfigChanged += config => _ = Task.Run(() => AtualizarEngineAsync(config, stoppingToken), stoppingToken);

        // Subscreve ao evento de reconexão do terminal — retenta engines que falharam
        _connectionManager.TerminalConectado += terminalId =>
            _ = Task.Run(() => RetentarEnginesPendentesAsync(terminalId, stoppingToken), stoppingToken);

        // Inicia engines para configs já carregados
        foreach (var config in _configWatcher.GetActiveConfigs())
            await AtualizarEngineAsync(config, stoppingToken);

        // Mantém vivo até cancelamento
        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task AtualizarEngineAsync(SymbolConfig config, CancellationToken ct)
    {
        await _lock.WaitAsync(ct);
        try
        {
            var engineKey = CriarEngineKey(config);

            // Remove engine existente se houver, inclusive quando o config foi desativado.
            if (_enginesPorSimbolo.TryGetValue(engineKey, out var existentes))
            {
                foreach (var existente in existentes)
                {
                    await existente.DisposeAsync();
                }
                _enginesPorSimbolo.Remove(engineKey);
                _logger.LogInformation("[{Simbolo}] {Count} StrategyEngine(s) encerrada(s) (config atualizado).", config.Symbol, existentes.Count);
            }

            if (!config.Operar)
            {
                _enginesPendentes.Remove(engineKey);
                _logger.LogInformation("[{Simbolo}] Operar=false; nenhuma StrategyEngine sera iniciada.", config.Symbol);
                return;
            }

            var gateway = _connectionManager.GetClient(config.TerminalId);
            var estrategiasList = new List<StrategyEngine>();

            // Configuração de fallback
            var estrategiasConfig = config.Estrategias;
            if (estrategiasConfig == null || !estrategiasConfig.Any())
            {
                estrategiasConfig = new List<Domain.ValueObjects.EstrategiaConfig>
                {
                    new Domain.ValueObjects.EstrategiaConfig(
                        Id: "cruzamento-ema",
                        Nome: "CruzamentoEma",
                        MagicNumber: 1,
                        Ativa: true,
                        Comprar: config.Comprar,
                        Vender: config.Vender,
                        Entrada: config.Entrada,
                        Saida: config.Saida,
                        GestaoDeRisco: config.GestaoDeRisco,
                        JanelaHorarioPermitido: config.GestaoDeRisco?.JanelaHorarioPermitido,
                        Indicadores: config.Indicadores,
                        Parametros: new Dictionary<string, object>())
                };
            }

            foreach (var estCfg in estrategiasConfig.Where(e => e.Ativa))
            {
                var engine = new StrategyEngine(
                    config, estCfg, gateway, _connectionManager, _catalogo, _catalogoMultiFonte, _catalogoEstrategias,
                    _riskGuard, _calculadoraLote, _execucao, _engineLogger);
                await engine.IniciarAsync(_marketData, ct);
                estrategiasList.Add(engine);
            }

            _enginesPorSimbolo[engineKey] = estrategiasList;
            _enginesPendentes.Remove(engineKey);

            _logger.LogInformation("[{Simbolo}] {Count} StrategyEngine(s) iniciada(s).", config.Symbol, estrategiasList.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar StrategyEngine para {Simbolo}.", config.Symbol);
            // Guarda como pendente para retentar após reconexão
            _enginesPendentes.Add(CriarEngineKey(config));
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task RetentarEnginesPendentesAsync(string terminalId, CancellationToken ct)
    {
        var pendentes = _configWatcher.GetActiveConfigs()
            .Where(c => c.TerminalId == terminalId && _enginesPendentes.Contains(CriarEngineKey(c)))
            .ToList();

        if (!pendentes.Any()) return;

        _logger.LogInformation("Terminal {TerminalId} reconectado — retentando {Count} engine(s) pendente(s).", terminalId, pendentes.Count);

        foreach (var config in pendentes)
            await AtualizarEngineAsync(config, ct);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var engines in _enginesPorSimbolo.Values)
        {
            foreach (var engine in engines)
            {
                await engine.DisposeAsync();
            }
        }

        _enginesPorSimbolo.Clear();
        await base.StopAsync(cancellationToken);
    }

    private static string CriarEngineKey(SymbolConfig config)
    {
        var brokerSymbol = config.BrokerSymbol ?? config.Symbol;
        return $"{config.TerminalId}:{config.Symbol}:{brokerSymbol}";
    }
}
