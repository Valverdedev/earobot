using Financial.Robot.Domain.ValueObjects;
using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>
/// Catálogo de indicadores registrados por nome.
/// Resolve IIndicador a partir do nome declarado em SymbolConfig.indicadores.
/// </summary>
public sealed class CatalogoIndicadores
{
    private readonly Dictionary<string, IIndicador> _catalogo;

    public CatalogoIndicadores()
    {
        _catalogo = new Dictionary<string, IIndicador>(StringComparer.OrdinalIgnoreCase)
        {
            [new RsiIndicador().Nome]  = new RsiIndicador(),
            [new EmaIndicador().Nome]  = new EmaIndicador(),
            [new AtrIndicador().Nome]  = new AtrIndicador(),
            [new MacdIndicador().Nome] = new MacdIndicador(),
            [new VwapIndicador().Nome] = new VwapIndicador(),
        };
    }

    /// <summary>Retorna o indicador pelo nome, ou null se não existir.</summary>
    public IIndicador? Resolver(string nome) =>
        _catalogo.TryGetValue(nome, out var indicador) ? indicador : null;

    /// <summary>Calcula todos os indicadores de um SymbolConfig a partir de uma série de candles.</summary>
    public IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> CalcularTodos(
        IEnumerable<IndicadorConfig> indicadores,
        IEnumerable<CandleMt5> candles)
    {
        var quotes = candles
            .Select(c => new Quote
            {
                Date   = c.Tempo,
                Open   = (decimal)c.Abertura,
                High   = (decimal)c.Maximo,
                Low    = (decimal)c.Minimo,
                Close  = (decimal)c.Fechamento,
                Volume = c.Volume
            })
            .OrderBy(q => q.Date)
            .ToList();

        var resultados = new List<(IndicadorConfig, ResultadoIndicador)>();

        foreach (var config in indicadores)
        {
            var indicador = Resolver(config.Nome);
            if (indicador is null) continue;

            var parametros = config.Parametros ?? new Dictionary<string, object>();
            var resultado  = indicador.Calcular(quotes, parametros);
            resultados.Add((config, resultado));
        }

        return resultados;
    }
}
