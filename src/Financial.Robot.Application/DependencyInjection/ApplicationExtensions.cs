using Financial.Robot.Application.Interfaces;
using Financial.Robot.Application.Services;
using Financial.Robot.Domain.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Financial.Robot.Application.DependencyInjection;

/// <summary>
/// Extensões de registro de DI para a camada Application.
/// </summary>
public static class ApplicationExtensions
{
    /// <summary>
    /// Registra todos os serviços da camada Application no container de DI.
    /// </summary>
    public static IServiceCollection AdicionarApplication(
        this IServiceCollection services)
    {
        services.AddScoped<IServicoConexaoMt5, ServicoConexaoMt5>();
        services.AddScoped<IServicoModoTeste, ServicoModoTeste>();
        services.AddSingleton<ServicoCalculoPreco>();

        return services;
    }
}
