using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy.Estrategias;

public sealed class OpeningRangeBreakout : IEstrategiaEntrada
{
    public string Nome => "OpeningRangeBreakout";

    public int ObterLookbackNecessario(EstrategiaConfig config)
    {
        var janelaFormacao = ParametroParser.ObterJanelaHorario(config.Parametros, "janelaFormacaoRange");
        var janelaOperacao = ParametroParser.ObterJanelaHorario(config.Parametros, "janelaOperacao");

        if (janelaFormacao is null || janelaOperacao is null)
            return 0;

        var diff = janelaOperacao.Value.Fim - janelaFormacao.Value.Inicio;
        if (diff < TimeSpan.Zero) diff = diff.Add(TimeSpan.FromDays(1));

        var minutos = (int)Math.Ceiling(diff.TotalMinutes);
        
        var tfStr = config.Indicadores?.FirstOrDefault()?.Timeframe ?? "M1";
        int tfMin = 1;

        if (tfStr.StartsWith("M", StringComparison.OrdinalIgnoreCase) && int.TryParse(tfStr.AsSpan(1), out var tm)) tfMin = tm;
        else if (tfStr.StartsWith("H", StringComparison.OrdinalIgnoreCase) && int.TryParse(tfStr.AsSpan(1), out var th)) tfMin = th * 60;

        return (int)Math.Ceiling((double)minutos / (tfMin > 0 ? tfMin : 1)) + 10;
    }

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var janelaFormacao = ParametroParser.ObterJanelaHorario(config.Parametros, "janelaFormacaoRange");
        var janelaOperacao = ParametroParser.ObterJanelaHorario(config.Parametros, "janelaOperacao");
        if (janelaFormacao is null || janelaOperacao is null)
            return ResultadoDecisao.Aguardar("janelaFormacaoRange/janelaOperacao não configuradas para ORB.");

        var tempoReferencia = tick.Timestamp;
        var horaAtual = tempoReferencia.TimeOfDay;

        var dentroOperacao = DentroDaJanela(horaAtual, janelaOperacao.Value);
        if (!dentroOperacao)
            return ResultadoDecisao.Aguardar("Fora da janela de operação do ORB.");

        DateTime formacaoInicioA = tempoReferencia.Date.Add(janelaFormacao.Value.Inicio);
        DateTime formacaoFimA = tempoReferencia.Date.Add(janelaFormacao.Value.Fim);
        if (formacaoFimA < formacaoInicioA) formacaoFimA = formacaoFimA.AddDays(1);

        DateTime formacaoInicioB = tempoReferencia.Date.AddDays(-1).Add(janelaFormacao.Value.Inicio);
        DateTime formacaoFimB = tempoReferencia.Date.AddDays(-1).Add(janelaFormacao.Value.Fim);
        if (formacaoFimB < formacaoInicioB) formacaoFimB = formacaoFimB.AddDays(1);

        DateTime inicioReal = formacaoInicioA <= tempoReferencia ? formacaoInicioA : formacaoInicioB;
        DateTime fimReal = formacaoInicioA <= tempoReferencia ? formacaoFimA : formacaoFimB;

        var candlesRange = candles.Where(c => c.Tempo >= inicioReal && c.Tempo <= fimReal).ToList();

        if (candlesRange.Count == 0)
            return ResultadoDecisao.Aguardar("Range de abertura ainda não formado (sem candles na janela de formação hoje).");

        var maxRange = candlesRange.Max(c => c.Maximo);
        var minRange = candlesRange.Min(c => c.Minimo);

        if (config.Comprar && tick.Ask > maxRange)
            return ResultadoDecisao.Comprar($"ORB: rompimento acima da máxima do range ({maxRange:F2})");

        if (config.Vender && tick.Bid < minRange)
            return ResultadoDecisao.Vender($"ORB: rompimento abaixo da mínima do range ({minRange:F2})");

        return ResultadoDecisao.Aguardar($"ORB: dentro do range ({minRange:F2}-{maxRange:F2}).");
    }

    private static bool DentroDaJanela(TimeSpan agora, (TimeSpan Inicio, TimeSpan Fim) janela) =>
        janela.Inicio <= janela.Fim
            ? agora >= janela.Inicio && agora <= janela.Fim
            : agora >= janela.Inicio || agora <= janela.Fim;
}
