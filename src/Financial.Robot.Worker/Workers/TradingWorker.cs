using Financial.Robot.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Workers;

/// <summary>
/// BackgroundService principal do sistema financial.robot.
/// Detecta o modo de execução via argumentos de linha de comando
/// e delega a execução ao serviço correspondente.
/// </summary>
public sealed class TradingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<TradingWorker> _logger;
    private readonly IConfiguration _configuration;

    /// <summary>Inicializa o worker com as dependências necessárias.</summary>
    public TradingWorker(
        IServiceProvider serviceProvider,
        IHostApplicationLifetime lifetime,
        ILogger<TradingWorker> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _lifetime = lifetime;
        _logger = logger;
        _configuration = configuration;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Aguarda o host inicializar completamente
        await Task.Yield();

        var modo = _configuration["mode"] ?? _configuration["Mode"] ?? "normal";

        _logger.LogInformation("TradingWorker iniciado — Modo: {Modo}", modo);

        try
        {
            if (modo.Equals("test", StringComparison.OrdinalIgnoreCase))
            {
                await ExecutarModoTesteAsync(stoppingToken);
                _logger.LogInformation("TradingWorker encerrando o host (modo test concluído)...");
                _lifetime.StopApplication();
            }
            else
            {
                _logger.LogInformation("Modo normal ativado. O robô ficará em execução aguardando configurações.");
                // Mantém o worker vivo no modo normal até que o cancelamento seja solicitado (Ctrl+C)
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TradingWorker cancelado pelo token de cancelamento.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal no TradingWorker.");
            _lifetime.StopApplication();
        }
    }

    private async Task ExecutarModoTesteAsync(CancellationToken ct)
    {
        _logger.LogInformation("Iniciando sequência do Modo Teste MT5...");

        using var scope = _serviceProvider.CreateScope();
        var servicoModoTeste = scope.ServiceProvider.GetRequiredService<IServicoModoTeste>();

        var resumo = await servicoModoTeste.ExecutarAsync(ct);

        if (resumo.Sucesso)
            _logger.LogInformation(
                "Modo Teste concluído com SUCESSO em {Duracao:g}.", resumo.DuracaoTotal);
        else
            _logger.LogError(
                "Modo Teste FALHOU no passo {Passo}: {Motivo}",
                resumo.PassoParada, resumo.MotivoParada);
    }
}
