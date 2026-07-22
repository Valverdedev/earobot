using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>Indicador RSI (Relative Strength Index) via Skender.</summary>
public sealed class RsiIndicador : IIndicador
{
    public string Nome => "RSI";

    public ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros)
    {
        var periodo = ParametroParser.ObterInt(parametros, "periodo", 14);
        var resultado = candles.GetRsi(periodo).LastOrDefault(r => r.Rsi.HasValue);

        return resultado is null
            ? new ResultadoIndicador(DateTime.UtcNow, null)
            : new ResultadoIndicador(resultado.Date, (double)resultado.Rsi!.Value);
    }
}
