using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

public sealed class ScalperWinPullbackCurto : IEstrategiaEntrada
{
    public string Nome => "ScalperWinPullbackCurto";

    public int ObterLookbackNecessario(EstrategiaConfig config) => 105;

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        if (candles.Count < 105)
            return ResultadoDecisao.Aguardar("Historico insuficiente (< 105 candles).");

        var niveis = ParametroParser.ObterNiveis(config.Parametros);
        if (niveis.Count == 0)
            return ResultadoDecisao.Aguardar("Nenhum nivel de suporte ou resistencia configurado.");

        var tolerancia = ParametroParser.ObterDouble(config.Parametros, "toleranciaNivelPreco", ParametroParser.ObterDouble(config.Parametros, "toleranciaNivelPontos", 35.0));
        var spreadMaximo = ParametroParser.ObterDouble(config.Parametros, "spreadMaximoPreco", ParametroParser.ObterDouble(config.Parametros, "spreadMaximoPontos", 10.0));
        var distMinVwap = ParametroParser.ObterDouble(config.Parametros, "distanciaMinimaVwapPreco", ParametroParser.ObterDouble(config.Parametros, "distanciaMinimaVwapPontos", 40.0));
        var rsiMaxVenda = ParametroParser.ObterDouble(config.Parametros, "rsiMaximoVenda", 55.0);
        var rsiMinCompra = ParametroParser.ObterDouble(config.Parametros, "rsiMinimoCompra", 40.0);

        var ultimo = candles[^1];

        var spreadAtual = tick.Ask - tick.Bid;
        if (spreadAtual > spreadMaximo)
            return ResultadoDecisao.Aguardar($"Spread muito alto ({spreadAtual:F1} > {spreadMaximo}).");

        var ema9M1 = ObterIndicador(indicadores, "EMA", "M1", 9);
        var rsiM1 = ObterIndicador(indicadores, "RSI", "M1");
        var vwapM1 = ObterIndicador(indicadores, "VWAP", "M1");

        if (ema9M1 == null || rsiM1 == null || vwapM1 == null)
            return ResultadoDecisao.Aguardar("Indicadores M1 (EMA, RSI ou VWAP) nao calculados.");

        var distVwap = Math.Abs(tick.Bid - vwapM1.Value);
        if (distVwap < distMinVwap)
            return ResultadoDecisao.Aguardar($"Preco muito proximo da VWAP (Dist: {distVwap:F1} < {distMinVwap}). Evitando chop.");

        var ema9M5 = ObterIndicador(indicadores, "EMA", "M5", 9);
        var ema21M5 = ObterIndicador(indicadores, "EMA", "M5", 21);
        if (ema9M5 == null || ema21M5 == null)
            return ResultadoDecisao.Aguardar("Indicadores M5 (EMA9/EMA21) nao calculados.");

        var m5Vendedor = tick.Bid < ema9M5.Value && tick.Bid < ema21M5.Value;
        var m5Comprador = tick.Bid > ema9M5.Value && tick.Bid > ema21M5.Value;

        if (config.Vender)
        {
            var testouResistencia = niveis.Any(n =>
                n.Tipo.Equals("resistencia", StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(ultimo.Maximo - n.Preco) <= tolerancia);

            var abaixoEma9M1 = ultimo.Fechamento < ema9M1.Value;
            if (testouResistencia && TemRejeicaoVendedora(ultimo) && abaixoEma9M1)
            {
                if (rsiM1.Value > rsiMaxVenda)
                    return ResultadoDecisao.Aguardar($"RSI M1 ({rsiM1.Value:F1}) acima do limite para venda ({rsiMaxVenda}).");

                if (!m5Vendedor)
                    return ResultadoDecisao.Aguardar("M5 nao confirma a venda (preco nao esta abaixo das duas EMAs curtas).");

                return ResultadoDecisao.Vender($"Pullback Curto M1 Venda. M5 alinhado, RSI={rsiM1.Value:F1}.");
            }
        }

        if (config.Comprar)
        {
            var testouSuporte = niveis.Any(n =>
                n.Tipo.Equals("suporte", StringComparison.OrdinalIgnoreCase) &&
                Math.Abs(ultimo.Minimo - n.Preco) <= tolerancia);

            var acimaEma9M1 = ultimo.Fechamento > ema9M1.Value;
            if (testouSuporte && TemRejeicaoCompradora(ultimo) && acimaEma9M1)
            {
                if (rsiM1.Value < rsiMinCompra)
                    return ResultadoDecisao.Aguardar($"RSI M1 ({rsiM1.Value:F1}) abaixo do limite para compra ({rsiMinCompra}).");

                if (!m5Comprador)
                    return ResultadoDecisao.Aguardar("M5 nao confirma a compra (preco nao esta acima das duas EMAs curtas).");

                return ResultadoDecisao.Comprar($"Pullback Curto M1 Compra. M5 alinhado, RSI={rsiM1.Value:F1}.");
            }
        }

        return ResultadoDecisao.Aguardar("Condicoes de pullback nao atingidas.");
    }

    private static double? ObterIndicador(
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        string nome,
        string timeframe,
        int? periodo = null)
    {
        return indicadores
            .FirstOrDefault(i =>
                i.Config.Nome.Equals(nome, StringComparison.OrdinalIgnoreCase) &&
                i.Config.Timeframe.Equals(timeframe, StringComparison.OrdinalIgnoreCase) &&
                (!periodo.HasValue || ParametroParser.ObterInt(i.Config.Parametros, "periodo", periodo.Value) == periodo.Value))
            .Resultado?.Valor;
    }

    private static bool TemRejeicaoVendedora(CandleMt5 candle)
    {
        var corpo = Math.Abs(candle.Fechamento - candle.Abertura);
        var sombraSuperior = candle.Maximo - Math.Max(candle.Abertura, candle.Fechamento);

        return corpo > 0 &&
               sombraSuperior >= corpo * 2 &&
               candle.Fechamento < candle.Abertura;
    }

    private static bool TemRejeicaoCompradora(CandleMt5 candle)
    {
        var corpo = Math.Abs(candle.Fechamento - candle.Abertura);
        var sombraInferior = Math.Min(candle.Abertura, candle.Fechamento) - candle.Minimo;

        return corpo > 0 &&
               sombraInferior >= corpo * 2 &&
               candle.Fechamento > candle.Abertura;
    }
}
