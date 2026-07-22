using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>Indicador EMA (Exponential Moving Average) via Skender.</summary>
public sealed class EmaIndicador : IIndicador
{
    public string Nome => "EMA";

    public ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros)
    {
        var periodo = ParametroParser.ObterInt(parametros, "periodo", 20);
        var resultado = candles.GetEma(periodo).LastOrDefault(r => r.Ema.HasValue);

        return resultado is null
            ? new ResultadoIndicador(DateTime.UtcNow, null)
            : new ResultadoIndicador(resultado.Date, (double)resultado.Ema!.Value);
    }
}
