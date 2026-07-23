using Financial.Robot.Application.Automation;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Financial.Robot.Worker.Automation;

/// <summary>Cliente SignalR que recebe config.apply da Bridge.Api e confirma aplicação local.</summary>
public sealed class AutomationSignalRClientService : BackgroundService
{
    private readonly IOptions<AutomationBridgeOptions> _options;
    private readonly ConfigApplyService _configApplyService;
    private readonly ILogger<AutomationSignalRClientService> _logger;
    private HubConnection? _connection;

    public AutomationSignalRClientService(
        IOptions<AutomationBridgeOptions> options,
        ConfigApplyService configApplyService,
        ILogger<AutomationSignalRClientService> logger)
    {
        _options = options;
        _configApplyService = configApplyService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("[Automation] Cliente SignalR desabilitado.");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.HubUrl))
        {
            _logger.LogWarning("[Automation] Cliente SignalR habilitado, mas AutomationBridge:HubUrl não foi configurado.");
            return;
        }

        _connection = CriarConexao(options);
        RegistrarHandlers(options, stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _connection.StartAsync(stoppingToken);
                _logger.LogInformation("[Automation] Conectado à Bridge.Api em {HubUrl}", options.HubUrl);
                await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
                return;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "[Automation] Falha ao conectar no hub. Nova tentativa em 10s.");
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
            await _connection.DisposeAsync();

        await base.StopAsync(cancellationToken);
    }

    private HubConnection CriarConexao(AutomationBridgeOptions options)
    {
        return new HubConnectionBuilder()
            .WithUrl(options.HubUrl, hubOptions =>
            {
                if (!string.IsNullOrWhiteSpace(options.DeviceToken))
                    hubOptions.AccessTokenProvider = () => Task.FromResult<string?>(options.DeviceToken);
            })
            .WithAutomaticReconnect()
            .Build();
    }

    private void RegistrarHandlers(AutomationBridgeOptions options, CancellationToken stoppingToken)
    {
        _connection!.On<ConfigApplyEnvelope>("ConfigUpdateRequested", async envelope =>
        {
            var resultado = await _configApplyService.AplicarAsync(envelope, options.ConfigRootPath, stoppingToken);
            await EnviarAckAsync(envelope, resultado, stoppingToken);
        });
    }

    private async Task EnviarAckAsync(
        ConfigApplyEnvelope envelope,
        ConfigApplyResult resultado,
        CancellationToken ct)
    {
        var agora = DateTimeOffset.UtcNow;
        var ack = new ConfigApplyAckEnvelope(
            SchemaVersion: "1.0",
            MessageId: $"ack_{envelope.MessageId}",
            CorrelationId: envelope.CorrelationId,
            Type: AutomationMessageTypes.ConfigApplyAck,
            Status: resultado.Status.ToString().ToLowerInvariant(),
            ReceivedAtUtc: agora,
            AppliedAtUtc: resultado.Status == ConfigApplyStatus.Applied ? agora : null,
            BackupId: resultado.BackupId,
            Errors: resultado.Errors.Select(e => new AutomationValidationError("$", e)).ToArray());

        if (_connection is null) return;
        await _connection.InvokeAsync("ConfigApplyAck", ack, ct);
    }
}
