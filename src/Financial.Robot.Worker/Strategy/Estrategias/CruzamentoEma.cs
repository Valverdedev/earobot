using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

public sealed class CruzamentoEma : IEstrategiaEntrada
{
    public string Nome => "CruzamentoEma";

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var emaRapida = indicadores.Where(r => r.Config.Nome.Equals("EMA", StringComparison.OrdinalIgnoreCase))
                                   .OrderBy(r => r.Config.Parametros?.TryGetValue("periodo", out var p) == true && p is int i ? i : 999)
                                   .ToList();

        if (emaRapida.Count < 2)
            return ResultadoDecisao.Aguardar("Faltam indicadores EMA configurados.");

        var ema9Val  = emaRapida[0].Resultado.Valor;
        var ema21Val = emaRapida[1].Resultado.Valor;
        var rsiVal   = indicadores.FirstOrDefault(r => r.Config.Nome.Equals("RSI", StringComparison.OrdinalIgnoreCase)).Resultado?.Valor;

        if (!ema9Val.HasValue || !ema21Val.HasValue)
            return ResultadoDecisao.Aguardar("Valores das EMAs não calculados.");

        var rsiOkCompra = rsiVal is null || (rsiVal >= 40 && rsiVal <= 70);
        var rsiOkVenda = rsiVal is null || (rsiVal >= 30 && rsiVal <= 60);

        // --- Calcula EMA do candle anterior para garantir que é um edge (cruzamento real) e não apenas nível ---
        if (candles.Count < 2)
            return ResultadoDecisao.Aguardar("Histórico insuficiente para cálculo edge-triggered.");

        var quotesAnteriores = candles.Take(candles.Count - 1).Select(c => new Quote
        {
            Date   = c.Tempo,
            Open   = (decimal)c.Abertura,
            High   = (decimal)c.Maximo,
            Low    = (decimal)c.Minimo,
            Close  = (decimal)c.Fechamento,
            Volume = c.Volume
        }).OrderBy(q => q.Date).ToList();

        var emaIndicador = new EmaIndicador();
        var ema9Anterior = emaIndicador.Calcular(quotesAnteriores, emaRapida[0].Config.Parametros ?? new Dictionary<string, object>()).Valor;
        var ema21Anterior = emaIndicador.Calcular(quotesAnteriores, emaRapida[1].Config.Parametros ?? new Dictionary<string, object>()).Valor;

        if (!ema9Anterior.HasValue || !ema21Anterior.HasValue)
            return ResultadoDecisao.Aguardar("Valores das EMAs anteriores não calculados.");

        var cruzouParaCima = ema9Anterior.Value <= ema21Anterior.Value && ema9Val > ema21Val;
        var cruzouParaBaixo = ema9Anterior.Value >= ema21Anterior.Value && ema9Val < ema21Val;

        if (config.Comprar && cruzouParaCima && rsiOkCompra)
        {
            var (bloqueia, motivo) = AvaliarForcaCesta(indicadoresMultiFonte, LadoOrdem.Compra);
            if (bloqueia) return ResultadoDecisao.Aguardar(motivo);
            return ResultadoDecisao.Comprar($"Cruzamento ALTA: [N-1: Rápida {ema9Anterior:F4} <= Lenta {ema21Anterior:F4}] -> [Atual: Rápida {ema9Val:F4} > Lenta {ema21Val:F4}]. RSI={rsiVal:F1} {motivo}");
        }

        if (config.Vender && cruzouParaBaixo && rsiOkVenda)
        {
            var (bloqueia, motivo) = AvaliarForcaCesta(indicadoresMultiFonte, LadoOrdem.Venda);
            if (bloqueia) return ResultadoDecisao.Aguardar(motivo);
            return ResultadoDecisao.Vender($"Cruzamento BAIXA: [N-1: Rápida {ema9Anterior:F4} >= Lenta {ema21Anterior:F4}] -> [Atual: Rápida {ema9Val:F4} < Lenta {ema21Val:F4}]. RSI={rsiVal:F1} {motivo}");
        }

        return ResultadoDecisao.Aguardar("Condições de cruzamento não atingidas.");
    }

    private (bool Bloqueia, string MotivoContexto) AvaliarForcaCesta(
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        LadoOrdem lado)
    {
        var forcaCesta = indicadoresMultiFonte.FirstOrDefault(r => r.Config.Nome.Equals("ForcaCesta", StringComparison.OrdinalIgnoreCase));
        if (forcaCesta.Resultado?.Valor is double score)
        {
            var limiarCompra = ObterDouble(forcaCesta.Config.Parametros, "limiarConfirmacaoCompra", 20);
            var limiarVenda  = ObterDouble(forcaCesta.Config.Parametros, "limiarConfirmacaoVenda", -20);
            var bloqueiaCfg  = ObterString(forcaCesta.Config.Parametros, "forcaCestaBloqueiaEntrada", "false");
            bool.TryParse(bloqueiaCfg, out var bloqueiaEntrada);

            if (lado == LadoOrdem.Compra && score < limiarCompra)
                return (bloqueiaEntrada, $"ForcaCesta={score:F1} < {limiarCompra}");
            
            if (lado == LadoOrdem.Venda && score > limiarVenda)
                return (bloqueiaEntrada, $"ForcaCesta={score:F1} > {limiarVenda}");
            
            return (false, $"[Confirmado: ForcaCesta={score:F1}]");
        }
        return (false, string.Empty);
    }

    private double ObterDouble(Dictionary<string, object>? dict, string key, double defaultValue)
    {
        if (dict != null && dict.TryGetValue(key, out var val))
        {
            if (val is double d) return d;
            if (val is int i) return i;
            if (val is string s && double.TryParse(s, out var parsed)) return parsed;
        }
        return defaultValue;
    }

    private string ObterString(Dictionary<string, object>? dict, string key, string defaultValue)
    {
        if (dict != null && dict.TryGetValue(key, out var val) && val is string s) return s;
        return defaultValue;
    }
}
