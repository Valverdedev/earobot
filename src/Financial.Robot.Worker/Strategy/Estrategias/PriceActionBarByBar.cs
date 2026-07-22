using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

/// <summary>
/// Estratégia de leitura pura de Price Action (sem indicadores), inspirada no método de Al Brooks:
/// identifica um impulso de tendência (sequência de trend bars), um pullback controlado e entra
/// na confirmação de rompimento do candle de sinal (fechamento além da máxima/mínima do sinal).
/// Um filtro de climax bloqueia entradas quando o impulso já teve uma barra de exaustão desproporcional.
/// </summary>
public sealed class PriceActionBarByBar : IEstrategiaEntrada
{
    public string Nome => "PriceActionBarByBar";

    public int ObterLookbackNecessario(EstrategiaConfig config)
    {
        var lookbackMedioRange = ParametroParser.ObterInt(config.Parametros, "lookbackMedioRange", 20);
        var candlesImpulsoMax = ParametroParser.ObterInt(config.Parametros, "candlesImpulsoMax", 6);
        var candlesPullbackMax = ParametroParser.ObterInt(config.Parametros, "candlesPullbackMax", 5);
        return lookbackMedioRange + candlesImpulsoMax + candlesPullbackMax + 5;
    }

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var p = ExtrairParametros(config.Parametros);

        var minimoNecessario = p.LookbackMedioRange + p.CandlesImpulsoMinimo + p.CandlesPullbackMinimo + 2;
        if (candles.Count < minimoNecessario)
            return ResultadoDecisao.Aguardar("Histórico insuficiente para leitura bar a bar.");

        var recentes = candles.OrderBy(c => c.Tempo).ToList();
        var atual = recentes[^1];

        var rangeMedio = CalcularRangeMedio(recentes, p.LookbackMedioRange);
        if (rangeMedio <= 0)
            return ResultadoDecisao.Aguardar("Range médio inválido para calibrar climax.");

        var padrao = EncontrarPadrao(recentes, p, rangeMedio);
        if (padrao is null)
            return ResultadoDecisao.Aguardar("Nenhum padrão de impulso + pullback + sinal identificado.");

        // Stop estrutural ao estilo Al Brooks: alguns ticks além do extremo oposto da barra de sinal
        // (o candle que confirmou o rompimento), não uma distância arbitrária de pips/ATR.
        var bufferStop = p.BufferStopPreco;

        if (padrao.Direcao == LadoOrdem.Compra)
        {
            if (!config.Comprar)
                return ResultadoDecisao.Aguardar("Sinal de compra identificado, mas estratégia não permite compra.");

            var rompeu = atual.Fechamento > padrao.BarraSinal.Maximo;
            if (!rompeu)
                return ResultadoDecisao.Aguardar("Aguardando fechamento além da máxima da barra de sinal.");

            var stopSugerido = padrao.BarraSinal.Minimo - bufferStop;
            return ResultadoDecisao.Comprar(
                $"PriceActionBarByBar: impulso de alta ({padrao.CandlesImpulso} barras) + pullback ({padrao.CandlesPullback} barras) " +
                $"+ rompimento da barra de sinal em {padrao.BarraSinal.Maximo:F2}.",
                stopSugerido);
        }
        else
        {
            if (!config.Vender)
                return ResultadoDecisao.Aguardar("Sinal de venda identificado, mas estratégia não permite venda.");

            var rompeu = atual.Fechamento < padrao.BarraSinal.Minimo;
            if (!rompeu)
                return ResultadoDecisao.Aguardar("Aguardando fechamento além da mínima da barra de sinal.");

            var stopSugerido = padrao.BarraSinal.Maximo + bufferStop;
            return ResultadoDecisao.Vender(
                $"PriceActionBarByBar: impulso de baixa ({padrao.CandlesImpulso} barras) + pullback ({padrao.CandlesPullback} barras) " +
                $"+ rompimento da barra de sinal em {padrao.BarraSinal.Minimo:F2}.",
                stopSugerido);
        }
    }

    // Varre janelas terminando na penúltima barra (a última é a barra "atual", candidata a confirmar o rompimento)
    // procurando: impulso (N+ trend bars na mesma direção) -> pullback (1..M barras que não devolvem
    // mais que percentualMaximoPullback do impulso) -> barra de sinal = última barra do pullback.
    private static PadraoEncontrado? EncontrarPadrao(List<CandleMt5> candles, Parametros p, double rangeMedio)
    {
        var indiceSinal = candles.Count - 2; // última barra fechada antes da barra "atual" em avaliação
        if (indiceSinal < p.CandlesImpulsoMinimo + p.CandlesPullbackMinimo)
            return null;

        for (var candlesPullback = p.CandlesPullbackMinimo; candlesPullback <= p.CandlesPullbackMax; candlesPullback++)
        {
            var inicioPullback = indiceSinal - candlesPullback + 1;
            if (inicioPullback - 1 < 0) continue;

            var fimImpulso = inicioPullback - 1;

            for (var candlesImpulso = p.CandlesImpulsoMinimo; candlesImpulso <= p.CandlesImpulsoMax; candlesImpulso++)
            {
                var inicioImpulso = fimImpulso - candlesImpulso + 1;
                if (inicioImpulso < 0) continue;

                var barrasImpulso = candles.GetRange(inicioImpulso, candlesImpulso);
                var direcao = ClassificarImpulso(barrasImpulso, p);
                if (direcao is null) continue;

                if (HouveClimax(barrasImpulso, rangeMedio, p.ClimaxMultiploRange))
                    continue;

                var topoImpulso = barrasImpulso.Max(c => c.Maximo);
                var fundoImpulso = barrasImpulso.Min(c => c.Minimo);
                var amplitudeImpulso = topoImpulso - fundoImpulso;
                if (amplitudeImpulso <= 0) continue;

                var barrasPullback = candles.GetRange(inicioPullback, candlesPullback);
                if (!PullbackValido(barrasPullback, direcao.Value, topoImpulso, fundoImpulso, amplitudeImpulso, p.PullbackMaximoPercentual))
                    continue;

                var barraSinal = barrasPullback[^1];
                return new PadraoEncontrado(direcao.Value, candlesImpulso, candlesPullback, barraSinal);
            }
        }

        return null;
    }

    // Trend bar: corpo representa ao menos p.CorpoMinimoFracaoRange do range total da barra.
    // Impulso: maioria das barras precisa ser trend bar na mesma direção, e a barra final do
    // impulso deve fechar no terço a favor da direção (sinal de convicção, não de exaustão isolada).
    private static LadoOrdem? ClassificarImpulso(List<CandleMt5> barras, Parametros p)
    {
        var trendBarsAlta = barras.Count(EhTrendBarAlta(p));
        var trendBarsBaixa = barras.Count(EhTrendBarBaixa(p));

        var maioriaAlta = trendBarsAlta >= barras.Count - (barras.Count / 3);
        var maioriaBaixa = trendBarsBaixa >= barras.Count - (barras.Count / 3);

        if (maioriaAlta && trendBarsAlta > trendBarsBaixa) return LadoOrdem.Compra;
        if (maioriaBaixa && trendBarsBaixa > trendBarsAlta) return LadoOrdem.Venda;
        return null;
    }

    private static Func<CandleMt5, bool> EhTrendBarAlta(Parametros p) => c =>
    {
        var range = c.Maximo - c.Minimo;
        if (range <= 0) return false;
        var corpo = c.Fechamento - c.Abertura;
        return corpo > 0 && corpo / range >= p.CorpoMinimoFracaoRange;
    };

    private static Func<CandleMt5, bool> EhTrendBarBaixa(Parametros p) => c =>
    {
        var range = c.Maximo - c.Minimo;
        if (range <= 0) return false;
        var corpo = c.Abertura - c.Fechamento;
        return corpo > 0 && corpo / range >= p.CorpoMinimoFracaoRange;
    };

    // Climax: alguma barra do impulso tem range muito maior que o range médio recente,
    // sinal de exaustão (blow-off) em vez de continuação saudável — bloqueia a entrada.
    private static bool HouveClimax(List<CandleMt5> barrasImpulso, double rangeMedio, double climaxMultiploRange) =>
        barrasImpulso.Any(c => (c.Maximo - c.Minimo) > rangeMedio * climaxMultiploRange);

    // Pullback válido: nenhuma barra do pullback devolve mais que percentualMaximoPullback
    // da amplitude do impulso, medido a partir do extremo do impulso.
    private static bool PullbackValido(
        List<CandleMt5> barrasPullback, LadoOrdem direcao,
        double topoImpulso, double fundoImpulso, double amplitudeImpulso, double percentualMaximoPullback)
    {
        var limite = amplitudeImpulso * percentualMaximoPullback;

        return direcao == LadoOrdem.Compra
            ? barrasPullback.All(c => topoImpulso - c.Minimo <= limite)
            : barrasPullback.All(c => c.Maximo - fundoImpulso <= limite);
    }

    private static double CalcularRangeMedio(List<CandleMt5> candles, int lookback)
    {
        var janela = candles.Count > lookback + 1
            ? candles.GetRange(candles.Count - lookback - 1, lookback)
            : candles.Take(Math.Max(0, candles.Count - 1)).ToList();

        return janela.Count == 0 ? 0 : janela.Average(c => c.Maximo - c.Minimo);
    }

    private static Parametros ExtrairParametros(IDictionary<string, object>? parametros) => new(
        ParametroParser.ObterDouble(parametros, "corpoMinimoFracaoRange", 0.55),
        ParametroParser.ObterInt(parametros, "candlesImpulsoMinimo", 2),
        ParametroParser.ObterInt(parametros, "candlesImpulsoMax", 6),
        ParametroParser.ObterInt(parametros, "candlesPullbackMinimo", 1),
        ParametroParser.ObterInt(parametros, "candlesPullbackMax", 5),
        ParametroParser.ObterDouble(parametros, "pullbackMaximoPercentual", 0.7),
        ParametroParser.ObterInt(parametros, "lookbackMedioRange", 20),
        ParametroParser.ObterDouble(parametros, "climaxMultiploRange", 2.5),
        ParametroParser.ObterDouble(parametros, "bufferStopPreco", 5.0)
    );

    private sealed record Parametros(
        double CorpoMinimoFracaoRange,
        int CandlesImpulsoMinimo,
        int CandlesImpulsoMax,
        int CandlesPullbackMinimo,
        int CandlesPullbackMax,
        double PullbackMaximoPercentual,
        int LookbackMedioRange,
        double ClimaxMultiploRange,
        double BufferStopPreco
    );

    private sealed record PadraoEncontrado(LadoOrdem Direcao, int CandlesImpulso, int CandlesPullback, CandleMt5 BarraSinal);
}
