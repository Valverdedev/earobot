using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Infrastructure.Gateways;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace Financial.Robot.Infrastructure.Mt5;

public class ConnectionManagerService : BackgroundService, IConnectionManager
{
    private readonly ILogger<ConnectionManagerService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    
    private readonly Dictionary<string, IGatewayMt5> _terminals = new();
    private readonly Dictionary<string, TerminalConfig> _terminalConfigs = new();

    /// <summary>Disparado quando um terminal reconecta com sucesso ao MT5.</summary>
    public event Action<string>? TerminalConectado;

    public ConnectionManagerService(
        ILogger<ConnectionManagerService> logger, 
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    public IGatewayMt5 GetClient(string terminalId)
    {
        if (_terminals.TryGetValue(terminalId, out var client))
        {
            return client;
        }
        
        throw new KeyNotFoundException($"Terminal '{terminalId}' não encontrado ou não configurado.");
    }

    public IEnumerable<string> GetConnectedTerminalIds()
    {
        return _terminals.Keys;
    }

    public IReadOnlyDictionary<string, IGatewayMt5> GetAllGateways()
    {
        return _terminals;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var terminalsSection = _configuration.GetSection("Terminals").Get<List<TerminalConfig>>();
        if (terminalsSection == null || terminalsSection.Count == 0)
        {
            _logger.LogWarning("Nenhum terminal configurado na seção 'Terminals' do appsettings.json.");
            return;
        }

        foreach (var config in terminalsSection)
        {
            if (string.IsNullOrWhiteSpace(config.TerminalId)) continue;
            
            _terminalConfigs[config.TerminalId] = config;
            
            // Criamos uma nova instância de GatewayMt5 para cada terminal configurado
            var gatewayLogger = _serviceProvider.GetRequiredService<ILogger<GatewayMt5>>();
            IGatewayMt5 gateway = new GatewayMt5(gatewayLogger);

            if (config.SomenteLeitura)
            {
                var somenteLeituraLogger = _serviceProvider.GetRequiredService<ILogger<GatewayMt5SomenteLeitura>>();
                gateway = new GatewayMt5SomenteLeitura(gateway, somenteLeituraLogger);
                _logger.LogInformation("Terminal {TerminalId} configurado como SOMENTE LEITURA.", config.TerminalId);
            }

            _terminals[config.TerminalId] = gateway;
        }

        _logger.LogInformation("ConnectionManager iniciado com {Count} terminais.", _terminals.Count);

        // Inicia a rotina de health check
        while (!stoppingToken.IsCancellationRequested)
        {
            await RealizarHealthCheckAsync(stoppingToken);
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task RealizarHealthCheckAsync(CancellationToken stoppingToken)
    {
        foreach (var kvp in _terminals)
        {
            var terminalId = kvp.Key;
            var gateway = kvp.Value;
            var config = _terminalConfigs[terminalId];

            if (!gateway.EstaConectado)
            {
                _logger.LogWarning("[{TerminalId}] Terminal desconectado. Tentando reconectar...", terminalId);
                try
                {
                    await gateway.ConectarAsync(config.Host, config.Port, timeoutSegundos: 5, stoppingToken);
                    _logger.LogInformation("[{TerminalId}] Reconexão bem sucedida.", terminalId);
                    TerminalConectado?.Invoke(terminalId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[{TerminalId}] Falha ao reconectar. Nova tentativa no próximo ciclo.", terminalId);
                }
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        foreach (var gateway in _terminals.Values)
        {
            await gateway.DesconectarAsync(cancellationToken);
        }
        await base.StopAsync(cancellationToken);
    }
}
