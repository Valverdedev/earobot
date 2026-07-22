using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Skender.Stock.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

/// <summary>
/// Estratégia de entrada baseada em micro tendências e pullbacks curtos utilizando três Médias Móveis Exponenciais (EMAs).
/// </summary>
public sealed class MicroTendenciaPullbackEma : IEstrategiaEntrada
{
    /// <summary>
    /// Nome identificador da estratégia.
    /// </summary>
    public string Nome => "MicroTendenciaPullbackEma";

    /// <summary>
    /// Define a quantidade mínima de histórico necessária para inicializar as médias móveis.
    /// </summary>
    public int ObterLookbackNecessario(EstrategiaConfig config)
    {
        var emaLenta = ParametroParser.ObterInt(config.Parametros, "emaLentaPeriodo", 50);
        var candlesInclinacao = ParametroParser.ObterInt(config.Parametros, "candlesInclinacao", 3);
        return emaLenta + candlesInclinacao + 150; 
    }

    /// <summary>
    /// Avalia as condições de mercado para emitir um sinal de compra, venda ou aguardar.
    /// </summary>
    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        if (candles.Count < 2)
            return ResultadoDecisao.Aguardar("Histórico insuficiente de candles.");

        var p = ExtrairParametros(config.Parametros);

        var spreadAtual = tick.Ask - tick.Bid;
        if (spreadAtual > p.SpreadMaximoPontos)
            return ResultadoDecisao.Aguardar($"Spread atual ({spreadAtual:F2}) excede o máximo permitido ({p.SpreadMaximoPontos}).");

        var quotes = ConverterParaQuotes(candles);
        var ctx = CalcularContextoGrafico(quotes, p);

        if (ctx.RsiSeries.Count == 0 || ctx.AtrSeries.Count == 0 || 
            ctx.EmaRapidaSeries.Count < p.CandlesInclinacao + 1 ||
            ctx.EmaMediaSeries.Count < p.CandlesInclinacao + 1 ||
            ctx.EmaLentaSeries.Count < p.CandlesInclinacao + 1)
            return ResultadoDecisao.Aguardar("Histórico de indicadores insuficiente.");

        if (p.UsarFiltroVwap && (ctx.VwapSeries == null || ctx.VwapSeries.Count == 0))
            return ResultadoDecisao.Aguardar("Histórico de VWAP vazio.");

        var erroVolatilidade = ValidarVolatilidade(candles[^1], ctx.AtrSeries[^1].Atr!.Value, p);
        if (erroVolatilidade != null) return erroVolatilidade;

        var analise = AnalisarTendenciaEAlinhamento(ctx, tick, p);

        if (!analise.CompraAlinhada && !analise.VendaAlinhada)
            return ResultadoDecisao.Aguardar("EMAs não alinhadas em tendência.");

        if (config.Comprar && analise.CompraAlinhada)
            return AvaliarCompra(candles, tick, p, ctx, analise);

        if (config.Vender && analise.VendaAlinhada)
            return AvaliarVenda(candles, tick, p, ctx, analise);

        return ResultadoDecisao.Aguardar("Condições não atendidas.");
    }

    private static ParametrosEstrategia ExtrairParametros(IDictionary<string, object>? parametros)
    {
        return new ParametrosEstrategia(
            ParametroParser.ObterInt(parametros, "emaRapidaPeriodo", 9),
            ParametroParser.ObterInt(parametros, "emaMediaPeriodo", 21),
            ParametroParser.ObterInt(parametros, "emaLentaPeriodo", 50),
            ParametroParser.ObterInt(parametros, "candlesInclinacao", 3),
            ParametroParser.ObterDouble(parametros, "toleranciaPullbackPreco", ParametroParser.ObterDouble(parametros, "toleranciaPullbackPontos", 40.0)),
            ParametroParser.ObterDouble(parametros, "rompimentoMinimoPreco", ParametroParser.ObterDouble(parametros, "rompimentoMinimoPontos", 5.0)),
            ParametroParser.ObterDouble(parametros, "spreadMaximoPreco", ParametroParser.ObterDouble(parametros, "spreadMaximoPontos", 15.0)),
            ParametroParser.ObterDouble(parametros, "atrMinimoPreco", ParametroParser.ObterDouble(parametros, "atrMinimoPontos", 30.0)),
            ParametroParser.ObterDouble(parametros, "candleMaxAtrMultiplo", 1.2),
            ParametroParser.ObterDouble(parametros, "rsiMaximoCompra", 68.0),
            ParametroParser.ObterDouble(parametros, "rsiMinimoVenda", 32.0),
            ParametroParser.ObterBool(parametros, "usarFiltroVwap", false),
            ParametroParser.ObterBool(parametros, "exigirFechamentoDirecional", true)
        );
    }

    private static List<Quote> ConverterParaQuotes(IReadOnlyList<CandleMt5> candles)
    {
        return candles.Select(c => new Quote
        {
            Date = c.Tempo,
            Open = (decimal)c.Abertura,
            High = (decimal)c.Maximo,
            Low = (decimal)c.Minimo,
            Close = (decimal)c.Fechamento,
            Volume = c.Volume
        }).OrderBy(q => q.Date).ToList();
    }

    private static ContextoGrafico CalcularContextoGrafico(List<Quote> quotes, ParametrosEstrategia p)
    {
        return new ContextoGrafico(
            quotes.GetEma(p.EmaRapidaPeriodo).Where(x => x.Ema.HasValue).ToList(),
            quotes.GetEma(p.EmaMediaPeriodo).Where(x => x.Ema.HasValue).ToList(),
            quotes.GetEma(p.EmaLentaPeriodo).Where(x => x.Ema.HasValue).ToList(),
            quotes.GetRsi(14).Where(x => x.Rsi.HasValue).ToList(),
            quotes.GetAtr(14).Where(x => x.Atr.HasValue).ToList(),
            p.UsarFiltroVwap ? quotes.Where(q => q.Date.Date == quotes[^1].Date.Date).GetVwap().Where(x => x.Vwap.HasValue).ToList() : null
        );
    }

    private static ResultadoDecisao? ValidarVolatilidade(CandleMt5 currentCandle, double currentAtr, ParametrosEstrategia p)
    {
        if (currentAtr < p.AtrMinimoPontos)
            return ResultadoDecisao.Aguardar($"ATR atual ({currentAtr:F2}) abaixo do mínimo exigido ({p.AtrMinimoPontos}).");

        var currentCandleSize = currentCandle.Maximo - currentCandle.Minimo;
        if (currentCandleSize > currentAtr * p.CandleMaxAtrMultiplo)
            return ResultadoDecisao.Aguardar($"Candle atual muito grande ({currentCandleSize:F2} > {currentAtr * p.CandleMaxAtrMultiplo:F2}).");

        return null;
    }

    private static AnaliseAlinhamento AnalisarTendenciaEAlinhamento(ContextoGrafico ctx, TickEvent tick, ParametrosEstrategia p)
    {
        var offset = 1 + p.CandlesInclinacao;
        
        var rAtual = ctx.EmaRapidaSeries[^1].Ema;
        var rAntigo = ctx.EmaRapidaSeries[^offset].Ema;
        var mAtual = ctx.EmaMediaSeries[^1].Ema;
        var mAntigo = ctx.EmaMediaSeries[^offset].Ema;
        var lAtual = ctx.EmaLentaSeries[^1].Ema;
        var lAntigo = ctx.EmaLentaSeries[^offset].Ema;

        bool tendenciaAlta = rAtual > rAntigo && mAtual > mAntigo && lAtual > lAntigo;
        bool tendenciaBaixa = rAtual < rAntigo && mAtual < mAntigo && lAtual < lAntigo;

        var atualRapida = (double)ctx.EmaRapidaSeries[^1].Ema!.Value;
        var atualMedia = (double)ctx.EmaMediaSeries[^1].Ema!.Value;
        var atualLenta = (double)ctx.EmaLentaSeries[^1].Ema!.Value;

        bool compra = tendenciaAlta && (atualRapida > atualMedia) && (atualMedia > atualLenta) && (tick.Bid > atualLenta);
        bool venda = tendenciaBaixa && (atualRapida < atualMedia) && (atualMedia < atualLenta) && (tick.Ask < atualLenta);

        return new AnaliseAlinhamento(compra, venda, atualRapida, atualMedia, atualLenta);
    }

    private static ResultadoDecisao AvaliarCompra(IReadOnlyList<CandleMt5> candles, TickEvent tick, ParametrosEstrategia p, ContextoGrafico ctx, AnaliseAlinhamento analise)
    {
        var currentCandle = candles[^1];
        var currentRsi = (double)ctx.RsiSeries[^1].Rsi!.Value;
        var currentAtr = (double)ctx.AtrSeries[^1].Atr!.Value;

        if (currentRsi > p.RsiMaximoCompra)
            return ResultadoDecisao.Aguardar($"RSI ({currentRsi:F2}) acima do limite para compra ({p.RsiMaximoCompra}).");

        if (p.UsarFiltroVwap && tick.Bid < (double)ctx.VwapSeries![^1].Vwap!.Value)
            return ResultadoDecisao.Aguardar("Preço abaixo da VWAP (filtro ativo).");

        bool pullbackValido = currentCandle.Minimo <= analise.AtualRapida + p.ToleranciaPullbackPontos && currentCandle.Minimo >= analise.AtualLenta - p.ToleranciaPullbackPontos;
        if (!pullbackValido)
            return ResultadoDecisao.Aguardar("Pullback não alcançou a zona ideal ou perdeu a EMA lenta.");

        if (p.ExigirFechamentoDirecional && currentCandle.Fechamento <= currentCandle.Abertura)
            return ResultadoDecisao.Aguardar("Aguardando candle de retomada fechar positivo.");

        bool gatilhoAcionado = currentCandle.Fechamento >= candles[^2].Maximo + p.RompimentoMinimoPontos || currentCandle.Fechamento >= analise.AtualRapida + p.RompimentoMinimoPontos;
        if (!gatilhoAcionado)
            return ResultadoDecisao.Aguardar("Gatilho de compra não acionado.");

        return ResultadoDecisao.Comprar($"Micro tendência de alta: Pullback confirmado e retomada (RSI {currentRsi:F2}, ATR {currentAtr:F2}).");
    }

    private static ResultadoDecisao AvaliarVenda(IReadOnlyList<CandleMt5> candles, TickEvent tick, ParametrosEstrategia p, ContextoGrafico ctx, AnaliseAlinhamento analise)
    {
        var currentCandle = candles[^1];
        var currentRsi = (double)ctx.RsiSeries[^1].Rsi!.Value;
        var currentAtr = (double)ctx.AtrSeries[^1].Atr!.Value;

        if (currentRsi < p.RsiMinimoVenda)
            return ResultadoDecisao.Aguardar($"RSI ({currentRsi:F2}) abaixo do limite para venda ({p.RsiMinimoVenda}).");

        if (p.UsarFiltroVwap && tick.Ask > (double)ctx.VwapSeries![^1].Vwap!.Value)
            return ResultadoDecisao.Aguardar("Preço acima da VWAP (filtro ativo).");

        bool pullbackValido = currentCandle.Maximo >= analise.AtualRapida - p.ToleranciaPullbackPontos && currentCandle.Maximo <= analise.AtualLenta + p.ToleranciaPullbackPontos;
        if (!pullbackValido)
            return ResultadoDecisao.Aguardar("Pullback não alcançou a zona ideal ou rompeu a EMA lenta.");

        if (p.ExigirFechamentoDirecional && currentCandle.Fechamento >= currentCandle.Abertura)
            return ResultadoDecisao.Aguardar("Aguardando candle de retomada fechar negativo.");

        bool gatilhoAcionado = currentCandle.Fechamento <= candles[^2].Minimo - p.RompimentoMinimoPontos || currentCandle.Fechamento <= analise.AtualRapida - p.RompimentoMinimoPontos;
        if (!gatilhoAcionado)
            return ResultadoDecisao.Aguardar("Gatilho de venda não acionado.");

        return ResultadoDecisao.Vender($"Micro tendência de baixa: Pullback confirmado e retomada (RSI {currentRsi:F2}, ATR {currentAtr:F2}).");
    }
}

internal sealed record ParametrosEstrategia(
    int EmaRapidaPeriodo, int EmaMediaPeriodo, int EmaLentaPeriodo, int CandlesInclinacao,
    double ToleranciaPullbackPontos, double RompimentoMinimoPontos, double SpreadMaximoPontos,
    double AtrMinimoPontos, double CandleMaxAtrMultiplo, double RsiMaximoCompra,
    double RsiMinimoVenda, bool UsarFiltroVwap, bool ExigirFechamentoDirecional
);

internal sealed record ContextoGrafico(
    List<EmaResult> EmaRapidaSeries, List<EmaResult> EmaMediaSeries, List<EmaResult> EmaLentaSeries,
    List<RsiResult> RsiSeries, List<AtrResult> AtrSeries, List<VwapResult>? VwapSeries
);

internal sealed record AnaliseAlinhamento(
    bool CompraAlinhada, bool VendaAlinhada, double AtualRapida, double AtualMedia, double AtualLenta
);
