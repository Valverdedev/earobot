using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>Indicador MACD via Skender.</summary>
public sealed class MacdIndicador : IIndicador
{
    public string Nome => "MACD";

    public ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros)
    {
        var resultado = candles.GetMacd().LastOrDefault(r => r.Macd.HasValue);

        return resultado is null
            ? new ResultadoIndicador(DateTime.UtcNow, null)
            : new ResultadoIndicador(
                resultado.Date,
                Valor:      resultado.Macd.HasValue      ? (double)resultado.Macd.Value      : null,
                Sinal:      resultado.Signal.HasValue    ? (double)resultado.Signal.Value    : null,
                Histograma: resultado.Histogram.HasValue ? (double)resultado.Histogram.Value : null
            );
    }
}
