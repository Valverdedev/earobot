using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Indicators;

/// <summary>Indicador VWAP (Volume Weighted Average Price) via Skender.</summary>
public sealed class VwapIndicador : IIndicador
{
    public string Nome => "VWAP";

    public ResultadoIndicador Calcular(IEnumerable<Quote> candles, IDictionary<string, object> parametros)
    {
        // Para VWAP intraday confiável, isolamos os candles apenas do dia atual do último candle disponível
        var ultimaData = candles.LastOrDefault()?.Date.Date;
        if (!ultimaData.HasValue)
            return new ResultadoIndicador(DateTime.UtcNow, null);

        var quotesDiaAtual = candles.Where(c => c.Date.Date == ultimaData.Value).ToList();

        if (quotesDiaAtual.Count == 0)
            return new ResultadoIndicador(DateTime.UtcNow, null);

        var resultado = quotesDiaAtual.GetVwap().LastOrDefault(r => r.Vwap.HasValue);

        return resultado is null
            ? new ResultadoIndicador(DateTime.UtcNow, null)
            : new ResultadoIndicador(resultado.Date, (double)resultado.Vwap!.Value);
    }
}
