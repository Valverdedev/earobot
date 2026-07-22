using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

public sealed class PriceActionSuporteResistencia : IEstrategiaEntrada
{
    public string Nome => "PriceActionSuporteResistencia";

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var niveis = ParametroParser.ObterNiveis(config.Parametros);
        if (niveis.Count == 0)
            return ResultadoDecisao.Aguardar("Nenhum nível de suporte/resistência configurado.");

        var tolerancia = ParametroParser.ObterDouble(config.Parametros, "toleranciaRompimentoPreco", ParametroParser.ObterDouble(config.Parametros, "toleranciaRompimentoPontos", 50));
        var exigeReteste = ParametroParser.ObterBool(config.Parametros, "exigeRetesteConfirmado", true);
        var candlesConfirmacao = ParametroParser.ObterInt(config.Parametros, "candlesConfirmacao", 2);

        if (candles.Count < candlesConfirmacao + 3)
            return ResultadoDecisao.Aguardar("Histórico insuficiente para avaliar Price Action.");

        var recentes = candles.OrderBy(c => c.Tempo).ToList();

        foreach (var (tipo, preco) in niveis)
        {
            var decisao = exigeReteste
                ? AvaliarRompimentoComReteste(recentes, tipo, preco, tolerancia, candlesConfirmacao, config)
                : AvaliarRejeicao(recentes, tipo, preco, tolerancia, config);

            if (decisao is not null) return decisao;
        }

        return ResultadoDecisao.Aguardar("Nenhum gatilho de Price Action nos níveis configurados.");
    }

    // Rompimento: candle fecha além do nível + tolerância; reteste: candle seguinte volta e testa sem fechar do lado errado;
    // confirmação: candle fecha na direção do rompimento original.
    private ResultadoDecisao? AvaliarRompimentoComReteste(
        List<CandleMt5> candles, string tipo, double nivel, double tolerancia, int candlesConfirmacao, EstrategiaConfig config)
    {
        // Procura, nos últimos (candlesConfirmacao + 2) candles, o padrão: rompimento -> reteste -> confirmação
        var janela = candles.TakeLast(candlesConfirmacao + 2).ToList();
        if (janela.Count < 3) return null;

        var rompimento = janela[0];
        var romperCima = rompimento.Fechamento > nivel + tolerancia && !string.Equals(tipo, "suporte", StringComparison.OrdinalIgnoreCase);
        var romperBaixo = rompimento.Fechamento < nivel - tolerancia && !string.Equals(tipo, "resistencia", StringComparison.OrdinalIgnoreCase);
        if (!romperCima && !romperBaixo) return null;

        var reteste = janela.Skip(1).Take(janela.Count - 2);
        var retesteValido = romperCima
            ? reteste.All(c => c.Minimo >= nivel - tolerancia)   // não voltou a fechar abaixo do nível
            : reteste.All(c => c.Maximo <= nivel + tolerancia);  // não voltou a fechar acima do nível
        if (!retesteValido) return null;

        var confirmacao = janela[^1];
        if (romperCima && confirmacao.Fechamento > rompimento.Maximo && config.Comprar)
            return ResultadoDecisao.Comprar($"PriceAction: rompimento+reteste confirmado acima de {nivel} ({tipo})");

        if (romperBaixo && confirmacao.Fechamento < rompimento.Minimo && config.Vender)
            return ResultadoDecisao.Vender($"PriceAction: rompimento+reteste confirmado abaixo de {nivel} ({tipo})");

        return null;
    }

    // Rejeição: candle mais recente tem pavio longo do lado do nível e fecha do lado oposto.
    private ResultadoDecisao? AvaliarRejeicao(List<CandleMt5> candles, string tipo, double nivel, double tolerancia, EstrategiaConfig config)
    {
        var ultimo = candles[^1];
        var perto = Math.Abs(ultimo.Minimo - nivel) <= tolerancia || Math.Abs(ultimo.Maximo - nivel) <= tolerancia;
        if (!perto) return null;

        var corpo = Math.Abs(ultimo.Fechamento - ultimo.Abertura);
        var pavioInferior = Math.Min(ultimo.Abertura, ultimo.Fechamento) - ultimo.Minimo;
        var pavioSuperior = ultimo.Maximo - Math.Max(ultimo.Abertura, ultimo.Fechamento);

        // Rejeição de baixa (suporte segurou): pavio inferior longo, fecha acima da abertura
        var permiteCompra = !string.Equals(tipo, "resistencia", StringComparison.OrdinalIgnoreCase);
        if (permiteCompra && pavioInferior > corpo * 1.5 && ultimo.Fechamento > ultimo.Abertura && config.Comprar)
            return ResultadoDecisao.Comprar($"PriceAction: rejeição de baixa no nível {nivel} ({tipo})");

        // Rejeição de alta (resistência segurou): pavio superior longo, fecha abaixo da abertura
        var permiteVenda = !string.Equals(tipo, "suporte", StringComparison.OrdinalIgnoreCase);
        if (permiteVenda && pavioSuperior > corpo * 1.5 && ultimo.Fechamento < ultimo.Abertura && config.Vender)
            return ResultadoDecisao.Vender($"PriceAction: rejeição de alta no nível {nivel} ({tipo})");

        return null;
    }
}
