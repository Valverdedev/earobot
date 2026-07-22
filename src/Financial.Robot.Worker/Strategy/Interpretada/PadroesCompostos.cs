using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public static class PadroesCompostos
{
    public static bool Avaliar(CondicaoDef def, ContextoAvaliacao ctx, out string rastro)
    {
        switch (def.Padrao)
        {
            case "impulsoPullback":
                return AvaliarImpulsoPullback(def, ctx, out rastro);
            case "rompimentoReteste":
                return AvaliarRompimentoReteste(def, ctx, out rastro);
            case "tocouNivel":
                return AvaliarTocouNivel(def, ctx, out rastro);
            default:
                rastro = $"{def.Padrao} FALHOU (padrão composto desconhecido)";
                return false;
        }
    }

    private static bool AvaliarImpulsoPullback(CondicaoDef def, ContextoAvaliacao ctx, out string rastro)
    {
        var candles = ctx.ObterHistoricoCandles().OrderBy(c => c.Tempo).ToList();
        
        var minImpulso = def.MinCandlesImpulso ?? 2;
        var maxImpulso = def.MaxCandlesImpulso ?? 6;
        var minPb = def.MinCandlesPullback ?? 1;
        var maxPb = def.MaxCandlesPullback ?? 5;
        var maxPbPercent = def.MaxPullbackPercentual ?? 0.7;
        var corpoMinimoFracao = def.CorpoMinimoFracaoRange ?? 0.55;

        int offsetSinal = def.InicioCandleSinal ?? 1;
        int maxOffsetSinal = def.FimCandleSinal ?? offsetSinal;
        
        var rangeMedio = CalcularRangeMedio(candles, 20);

        for (int offset = offsetSinal; offset <= maxOffsetSinal; offset++)
        {
            var indiceSinal = candles.Count - 1 - offset;
            if (indiceSinal < minImpulso + minPb - 1) continue;
            
            for (var candlesPullback = minPb; candlesPullback <= maxPb; candlesPullback++)
            {
                var inicioPullback = indiceSinal - candlesPullback + 1;
                if (inicioPullback < 0) continue;

                var fimImpulso = inicioPullback - 1;

                for (var candlesImpulso = minImpulso; candlesImpulso <= maxImpulso; candlesImpulso++)
                {
                    var inicioImpulso = fimImpulso - candlesImpulso + 1;
                    if (inicioImpulso < 0) continue;

                    var barrasImpulso = candles.GetRange(inicioImpulso, candlesImpulso);
                    var direcao = ClassificarImpulso(barrasImpulso, corpoMinimoFracao);
                    
                    if (direcao is null) continue;
                    if (def.Lado == "alta" && direcao != LadoOrdem.Compra) continue;
                    if (def.Lado == "baixa" && direcao != LadoOrdem.Venda) continue;

                    // Filtro de Climax
                    if (def.ClimaxMultiploRange.HasValue && def.ClimaxMultiploRange.Value > 0)
                    {
                        var lookbackMedio = def.LookbackMedioRange ?? 20;
                        var rangeMedioGlobal = CalcularRangeMedio(candles, lookbackMedio);
                        if (rangeMedioGlobal > 0 && barrasImpulso.Any(c => (c.Maximo - c.Minimo) > rangeMedioGlobal * def.ClimaxMultiploRange.Value))
                            continue;
                    }

                    var topoImpulso = barrasImpulso.Max(c => c.Maximo);
                    var fundoImpulso = barrasImpulso.Min(c => c.Minimo);
                    var amplitudeImpulso = topoImpulso - fundoImpulso;
                    if (amplitudeImpulso <= 0) continue;

                    var barrasPullback = candles.GetRange(inicioPullback, candlesPullback);
                    if (!PullbackValido(barrasPullback, direcao.Value, topoImpulso, fundoImpulso, amplitudeImpulso, maxPbPercent))
                        continue;

                    var barraSinal = barrasPullback[^1];
                    
                    if (!string.IsNullOrEmpty(def.IdRef))
                        ctx.RegistrarRef(def.IdRef, barraSinal);

                    rastro = $"impulsoPullback OK (imp={candlesImpulso}, pb={candlesPullback}, sinal=[{offset}], lado={def.Lado})";
                    return true;
                }
            }
        }
        
        rastro = "impulsoPullback FALHOU (padrão não encontrado)";
        return false;
    }

    private static double CalcularRangeMedio(List<CandleMt5> candles, int lookback)
    {
        var janela = candles.Count > lookback + 1
            ? candles.GetRange(candles.Count - lookback - 1, lookback)
            : candles.Take(Math.Max(0, candles.Count - 1)).ToList();

        return janela.Count == 0 ? 0 : janela.Average(c => c.Maximo - c.Minimo);
    }

    private static LadoOrdem? ClassificarImpulso(List<CandleMt5> barras, double corpoMinimoFracao)
    {
        var trendBarsAlta = barras.Count(c => EhTrendBarAlta(c, corpoMinimoFracao));
        var trendBarsBaixa = barras.Count(c => EhTrendBarBaixa(c, corpoMinimoFracao));

        var maioriaAlta = trendBarsAlta >= barras.Count - (barras.Count / 3);
        var maioriaBaixa = trendBarsBaixa >= barras.Count - (barras.Count / 3);

        if (maioriaAlta && trendBarsAlta > trendBarsBaixa) return LadoOrdem.Compra;
        if (maioriaBaixa && trendBarsBaixa > trendBarsAlta) return LadoOrdem.Venda;
        return null;
    }

    private static bool EhTrendBarAlta(CandleMt5 c, double fracao)
    {
        var range = c.Maximo - c.Minimo;
        if (range <= 0) return false;
        var corpo = c.Fechamento - c.Abertura;
        return corpo > 0 && corpo / range >= fracao;
    }

    private static bool EhTrendBarBaixa(CandleMt5 c, double fracao)
    {
        var range = c.Maximo - c.Minimo;
        if (range <= 0) return false;
        var corpo = c.Abertura - c.Fechamento;
        return corpo > 0 && corpo / range >= fracao;
    }

    private static bool PullbackValido(
        List<CandleMt5> barrasPullback, LadoOrdem direcao,
        double topoImpulso, double fundoImpulso, double amplitudeImpulso, double percentualMaximoPullback)
    {
        var limite = amplitudeImpulso * percentualMaximoPullback;
        return direcao == LadoOrdem.Compra
            ? barrasPullback.All(c => topoImpulso - c.Minimo <= limite)
            : barrasPullback.All(c => c.Maximo - fundoImpulso <= limite);
    }

    private static bool AvaliarRompimentoReteste(CondicaoDef def, ContextoAvaliacao ctx, out string rastro)
    {
        int maxIndice = def.FimCandleSinal ?? 5;
        var opNivel = new Operando { Fonte = def.TipoNivel == "suporte" ? TipoFonte.NivelSuportePreco : TipoFonte.NivelResistenciaPreco };
        var valorNivel = ctx.ResolverOperando(opNivel);

        if (valorNivel == null)
        {
            rastro = $"rompimentoReteste FALHOU (nível {def.TipoNivel} não encontrado)";
            return false;
        }

        CandleMt5? rompimento = null;
        int idxRomp = -1;
        var tolerancia = def.ToleranciaPreco ?? 0;

        for (int i = 1; i <= maxIndice; i++)
        {
            var c = ctx.ObterCandle(i);
            if (c == null) continue;

            if (def.Lado == "alta" && c.Fechamento > valorNivel + tolerancia && c.Abertura <= valorNivel + tolerancia)
            {
                rompimento = c;
                idxRomp = i;
                break;
            }
            else if (def.Lado == "baixa" && c.Fechamento < valorNivel - tolerancia && c.Abertura >= valorNivel - tolerancia)
            {
                rompimento = c;
                idxRomp = i;
                break;
            }
        }

        if (rompimento == null)
        {
            rastro = "rompimentoReteste FALHOU (rompimento não encontrado)";
            return false;
        }

        bool devolveu = false;
        for (int i = 0; i < idxRomp; i++)
        {
            var c = ctx.ObterCandle(i);
            if (c == null) continue;
            
            if (def.Lado == "alta" && c.Fechamento < valorNivel) devolveu = true;
            if (def.Lado == "baixa" && c.Fechamento > valorNivel) devolveu = true;
        }

        if (devolveu)
        {
            rastro = "rompimentoReteste FALHOU (rompimento devolvido)";
            return false;
        }

        if (!string.IsNullOrEmpty(def.IdRef))
            ctx.RegistrarRef(def.IdRef, rompimento);

        rastro = $"rompimentoReteste OK (romp=[{idxRomp}], nivel={valorNivel:F2})";
        return true;
    }

    private static bool AvaliarTocouNivel(CondicaoDef def, ContextoAvaliacao ctx, out string rastro)
    {
        var opNivel = new Operando { Fonte = def.TipoNivel == "suporte" ? TipoFonte.NivelSuportePreco : TipoFonte.NivelResistenciaPreco };
        var valorNivel = ctx.ResolverOperando(opNivel);

        if (valorNivel == null)
        {
            rastro = $"tocouNivel FALHOU (nível {def.TipoNivel} não encontrado)";
            return false;
        }

        var tolerancia = def.ToleranciaPreco ?? 0;
        int maxCandles = def.FimCandleSinal ?? 3;
        CandleMt5? tocou = null;
        int idxTocou = -1;

        for (int i = 0; i <= maxCandles; i++)
        {
            var c = ctx.ObterCandle(i);
            if (c == null) continue;

            bool tocouAqui = false;
            if (def.Lado == "minimo")
            {
                tocouAqui = c.Minimo <= valorNivel + tolerancia && c.Minimo >= valorNivel - tolerancia;
            }
            else if (def.Lado == "maximo")
            {
                tocouAqui = c.Maximo <= valorNivel + tolerancia && c.Maximo >= valorNivel - tolerancia;
            }
            else
            {
                tocouAqui = c.Minimo <= valorNivel + tolerancia && c.Maximo >= valorNivel - tolerancia;
            }

            if (tocouAqui)
            {
                tocou = c;
                idxTocou = i;
                break;
            }
        }

        if (tocou == null)
        {
            rastro = $"tocouNivel FALHOU (nenhum toque nos ultimos {maxCandles} candles)";
            return false;
        }

        if (!string.IsNullOrEmpty(def.IdRef))
            ctx.RegistrarRef(def.IdRef, tocou);

        rastro = $"tocouNivel OK (tocou=[{idxTocou}], nivel={valorNivel:F2})";
        return true;
    }
}
