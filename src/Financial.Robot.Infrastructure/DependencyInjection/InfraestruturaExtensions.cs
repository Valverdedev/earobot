using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Infrastructure.Gateways;
using Financial.Robot.Infrastructure.Repositorios;
using Microsoft.Extensions.DependencyInjection;

namespace Financial.Robot.Infrastructure.DependencyInjection;

/// <summary>
/// Extensões de registro de DI para a camada Infrastructure.
/// </summary>
public static class InfraestruturaExtensions
{
    /// <summary>
    /// Registra todos os serviços da camada Infrastructure no container de DI.
    /// </summary>
    public static IServiceCollection AdicionarInfraestrutura(
        this IServiceCollection services)
    {
        // Connection Manager gerencia os gateways (terminais MT5)
        services.AddSingleton<Financial.Robot.Infrastructure.Mt5.ConnectionManagerService>();
        services.AddSingleton<Financial.Robot.Application.Interfaces.IConnectionManager>(sp => sp.GetRequiredService<Financial.Robot.Infrastructure.Mt5.ConnectionManagerService>());
        services.AddHostedService(sp => sp.GetRequiredService<Financial.Robot.Infrastructure.Mt5.ConnectionManagerService>());

        // Repositórios in-memory — Singleton para manter estado durante a execução do teste
        services.AddSingleton<IRepositorioPosicao, RepositorioPosicaoEmMemoria>();
        services.AddSingleton<IRepositorioOrdemPendente, RepositorioOrdemPendenteEmMemoria>();

        return services;
    }
}
