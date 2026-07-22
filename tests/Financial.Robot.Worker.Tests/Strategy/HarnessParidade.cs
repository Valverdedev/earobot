using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Financial.Robot.Worker.Tests.Strategy;

public static class HarnessParidade
{
    public static void AvaliarParidade(
        IEstrategiaEntrada estrategiaCompilada,
        IEstrategiaEntrada estrategiaInterpretada,
        List<CandleMt5> historico,
        EstrategiaConfig configCompilada,
        EstrategiaConfig configInterpretada,
        bool injetarRsi14 = false)
    {
        int lookbackCompilada = estrategiaCompilada.ObterLookbackNecessario(configCompilada);
        int lookbackInterpretada = estrategiaInterpretada.ObterLookbackNecessario(configInterpretada);
        int maxLookback = Math.Max(lookbackCompilada, lookbackInterpretada);

        int divergencias = 0;

        var quotes = historico.Select(c => new Quote
        {
            Date = c.Tempo,
            Open = (decimal)c.Abertura,
            High = (decimal)c.Maximo,
            Low = (decimal)c.Minimo,
            Close = (decimal)c.Fechamento,
            Volume = c.Volume
        }).ToList();
        
        var rsiResults = injetarRsi14 ? quotes.GetRsi(14).ToList() : null;

        var sb = new System.Text.StringBuilder();

        for (int i = maxLookback + 1; i <= historico.Count; i++)
        {
            var janela = historico.Take(i).ToList();
            var atual = janela[^1];
            var tick = new TickEvent("T", "WIN", atual.Fechamento, atual.Fechamento, atual.Tempo);

            List<(IndicadorConfig, ResultadoIndicador)> indicadores = new();
            if (injetarRsi14 && rsiResults != null)
            {
                var val = rsiResults[i - 1].Rsi;
                indicadores.Add((new IndicadorConfig("RSI", "M1", new Dictionary<string, object>()), new ResultadoIndicador(atual.Tempo, (double?)val)));
            }

            var resCompilada = estrategiaCompilada.Avaliar(janela, indicadores, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), tick, configCompilada);
            var resInterpretada = estrategiaInterpretada.Avaliar(janela, indicadores, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), tick, configInterpretada);

            bool diffExecutar = resCompilada.Executar != resInterpretada.Executar;
            bool diffLado = resCompilada.Lado != resInterpretada.Lado;
            bool diffStop = false;
            
            if (resCompilada.StopSugerido.HasValue && resInterpretada.StopSugerido.HasValue)
            {
                if (Math.Abs(resCompilada.StopSugerido.Value - resInterpretada.StopSugerido.Value) > 1e-9)
                    diffStop = true;
            }
            else if (resCompilada.StopSugerido.HasValue != resInterpretada.StopSugerido.HasValue)
            {
                diffStop = true;
            }

            if (diffExecutar || diffLado || diffStop)
            {
                divergencias++;
                if (divergencias <= 20)
                {
                    sb.AppendLine($"\nDivergência no candle {i} ({janela[^1].Tempo}):");
                    sb.AppendLine($"  Compilada: Executar={resCompilada.Executar}, Lado={resCompilada.Lado}, Stop={resCompilada.StopSugerido} | Motivo: {resCompilada.Motivo}");
                    sb.AppendLine($"  Interpretada: Executar={resInterpretada.Executar}, Lado={resInterpretada.Lado}, Stop={resInterpretada.StopSugerido} | Motivo: {resInterpretada.Motivo}");
                }
            }
        }

        divergencias.Should().Be(0, "A paridade deve ser de 100% entre a estratégia compilada e a interpretada. Detalhes:\n" + sb.ToString());
    }

    public static List<CandleMt5> GerarSerieRealista(int dias, double precoInicial = 100000)
    {
        var random = new Random(42);
        var candles = new List<CandleMt5>();
        var t = new DateTime(2026, 1, 1, 9, 0, 0, DateTimeKind.Utc);
        double preco = precoInicial;

        for (int d = 0; d < dias; d++)
        {
            if (d > 0)
            {
                // Market gap to next day 09:00
                t = t.Date.AddDays(1).AddHours(9);
                preco += (random.NextDouble() * 1000) - 500;
            }

            double volDia = 10 + random.NextDouble() * 40;

            // Session from 09:00 to 18:00 (540 minutes)
            for (int m = 0; m < 540; m++)
            {
                double variacao = (random.NextDouble() * 2 * volDia) - volDia;
                double abertura = preco;
                double fechamento = abertura + variacao;

                double maximo = Math.Max(abertura, fechamento) + random.NextDouble() * volDia;
                double minimo = Math.Min(abertura, fechamento) - random.NextDouble() * volDia;

                if (random.NextDouble() < 0.05)
                {
                    fechamento = abertura + (random.NextDouble() * 4 - 2); 
                }

                candles.Add(new CandleMt5(t, abertura, maximo, minimo, fechamento, 1000));
                preco = fechamento;
                t = t.AddMinutes(1);
            }
        }
        
        // Add specific patterns for stress test at the end of the series
        // 1. Clean trend with pullback
        for (int i = 0; i < 4; i++) {
            candles.Add(new CandleMt5(t, preco, preco + 100, preco, preco + 90, 1000));
            preco += 90; t = t.AddMinutes(1);
        }
        for (int i = 0; i < 2; i++) {
            candles.Add(new CandleMt5(t, preco, preco + 20, preco - 40, preco - 30, 1000));
            preco -= 30; t = t.AddMinutes(1);
        }
        // Breakout candle
        candles.Add(new CandleMt5(t, preco, preco + 150, preco, preco + 140, 1000));
        preco += 140; t = t.AddMinutes(1);

        // 2. Climax
        candles.Add(new CandleMt5(t, preco, preco + 600, preco, preco + 550, 1000));
        preco += 550; t = t.AddMinutes(1);

        return candles;
    }
}
