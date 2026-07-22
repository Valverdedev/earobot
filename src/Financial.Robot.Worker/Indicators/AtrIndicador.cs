using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>Indicador ATR (Average True Range) via Skender.</summary>
public sealed class AtrIndicador : IIndicador
{
    public string Nome => "ATR";

    public ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros)
    {
        var periodo = ParametroParser.ObterInt(parametros, "periodo", 14);
        var resultado = candles.GetAtr(periodo).LastOrDefault(r => r.Atr.HasValue);

        return resultado is null
            ? new ResultadoIndicador(DateTime.UtcNow, null)
            : new ResultadoIndicador(resultado.Date, (double)resultado.Atr!.Value);
    }
}
