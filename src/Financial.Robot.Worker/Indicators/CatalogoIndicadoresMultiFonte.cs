using Financial.Robot.Domain.ValueObjects;

using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Indicators;

/// <summary>
/// Catálogo de indicadores multifonte registrados por nome.
/// </summary>
public sealed class CatalogoIndicadoresMultiFonte
{
    private readonly Dictionary<string, IIndicadorMultiFonte> _catalogo;

    public CatalogoIndicadoresMultiFonte(ILoggerFactory loggerFactory)
    {
        _catalogo = new Dictionary<string, IIndicadorMultiFonte>(StringComparer.OrdinalIgnoreCase)
        {
            [new ForcaCestaIndicador(loggerFactory.CreateLogger<ForcaCestaIndicador>()).Nome] = new ForcaCestaIndicador(loggerFactory.CreateLogger<ForcaCestaIndicador>())
        };
    }

    public IIndicadorMultiFonte? Resolver(string nome) =>
        _catalogo.TryGetValue(nome, out var indicador) ? indicador : null;

    public async Task<IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)>> CalcularTodosAsync(
        IEnumerable<IndicadorConfig> indicadores,
        IReadOnlyDictionary<string, Domain.Interfaces.IGatewayMt5> gatewaysPorTerminal,
        CancellationToken ct)
    {
        var resultados = new List<(IndicadorConfig, ResultadoIndicador)>();

        foreach (var config in indicadores)
        {
            var indicador = Resolver(config.Nome);
            if (indicador is null) continue;

            var resultado = await indicador.CalcularAsync(gatewaysPorTerminal, config.Parametros, ct);
            resultados.Add((config, resultado));
        }

        return resultados;
    }
}
