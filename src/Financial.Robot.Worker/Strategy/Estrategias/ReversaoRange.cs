using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

public sealed class ReversaoRange : IEstrategiaEntrada
{
    public string Nome => "ReversaoRange";

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var niveis = ParametroParser.ObterNiveis(config.Parametros);
        if (niveis.Count == 0)
            return ResultadoDecisao.Aguardar("Nenhum nível configurado para ReversaoRange.");

        var rsiVal = indicadores.FirstOrDefault(r => r.Config.Nome.Equals("RSI", StringComparison.OrdinalIgnoreCase)).Resultado?.Valor;
        if (!rsiVal.HasValue)
            return ResultadoDecisao.Aguardar("RSI não calculado — necessário para ReversaoRange.");

        var rsiSobrevenda = ParametroParser.ObterDouble(config.Parametros, "rsiSobrevendaMaximo", 30);
        var rsiSobrecompra = ParametroParser.ObterDouble(config.Parametros, "rsiSobrecompraMinimo", 70);
        var distanciaMaxima = ParametroParser.ObterDouble(config.Parametros, "distanciaMaximaDoNivelPontos", 100);

        var suporte = niveis.Where(n => n.Tipo.Equals("suporte", StringComparison.OrdinalIgnoreCase))
                             .OrderBy(n => Math.Abs(tick.Bid - n.Preco)).FirstOrDefault();
        var resistencia = niveis.Where(n => n.Tipo.Equals("resistencia", StringComparison.OrdinalIgnoreCase))
                                 .OrderBy(n => Math.Abs(tick.Bid - n.Preco)).FirstOrDefault();

        if (config.Comprar && suporte != default &&
            Math.Abs(tick.Bid - suporte.Preco) <= distanciaMaxima &&
            rsiVal <= rsiSobrevenda)
        {
            return ResultadoDecisao.Comprar($"ReversaoRange: RSI={rsiVal:F1} (sobrevenda) próximo ao suporte {suporte.Preco}");
        }

        if (config.Vender && resistencia != default &&
            Math.Abs(tick.Bid - resistencia.Preco) <= distanciaMaxima &&
            rsiVal >= rsiSobrecompra)
        {
            return ResultadoDecisao.Vender($"ReversaoRange: RSI={rsiVal:F1} (sobrecompra) próximo à resistência {resistencia.Preco}");
        }

        return ResultadoDecisao.Aguardar($"ReversaoRange: RSI={rsiVal:F1}, fora de condição de reversão.");
    }
}
