using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.ValueObjects;
using Skender.Stock.Indicators;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Financial.Robot.Worker.Indicators;

public static class AgregadorTimeframe
{
    public static List<Quote> AgruparM1ParaTimeframeMaior(IReadOnlyList<CandleMt5> candlesBase, string timeframeConfirmacao)
    {
        var tfQuotes = new List<Quote>();
        if (candlesBase.Count == 0) return tfQuotes;

        DateTime chaveAtual = ObterChaveTimeframe(candlesBase[0].Tempo, timeframeConfirmacao);
        decimal open = (decimal)candlesBase[0].Abertura;
        decimal high = (decimal)candlesBase[0].Maximo;
        decimal low = (decimal)candlesBase[0].Minimo;
        decimal close = (decimal)candlesBase[0].Fechamento;
        decimal volume = (decimal)candlesBase[0].Volume;

        for (int i = 1; i < candlesBase.Count; i++)
        {
            var c = candlesBase[i];
            DateTime chave = ObterChaveTimeframe(c.Tempo, timeframeConfirmacao);

            if (chave == chaveAtual)
            {
                if ((decimal)c.Maximo > high) high = (decimal)c.Maximo;
                if ((decimal)c.Minimo < low) low = (decimal)c.Minimo;
                close = (decimal)c.Fechamento;
                volume += (decimal)c.Volume;
            }
            else
            {
                tfQuotes.Add(new Quote
                {
                    Date = chaveAtual,
                    Open = open,
                    High = high,
                    Low = low,
                    Close = close,
                    Volume = volume
                });

                chaveAtual = chave;
                open = (decimal)c.Abertura;
                high = (decimal)c.Maximo;
                low = (decimal)c.Minimo;
                close = (decimal)c.Fechamento;
                volume = (decimal)c.Volume;
            }
        }

        tfQuotes.Add(new Quote
        {
            Date = chaveAtual,
            Open = open,
            High = high,
            Low = low,
            Close = close,
            Volume = volume
        });

        return tfQuotes;
    }

    public static DateTime ObterChaveTimeframe(DateTime tempo, string timeframe)
    {
        return timeframe.ToUpperInvariant() switch
        {
            "M1" => new DateTime(tempo.Year, tempo.Month, tempo.Day, tempo.Hour, tempo.Minute, 0),
            "M5" => new DateTime(tempo.Year, tempo.Month, tempo.Day, tempo.Hour, (tempo.Minute / 5) * 5, 0),
            "M15" => new DateTime(tempo.Year, tempo.Month, tempo.Day, tempo.Hour, (tempo.Minute / 15) * 15, 0),
            "M30" => new DateTime(tempo.Year, tempo.Month, tempo.Day, tempo.Hour, (tempo.Minute / 30) * 30, 0),
            "H1" => new DateTime(tempo.Year, tempo.Month, tempo.Day, tempo.Hour, 0, 0),
            "H4" => new DateTime(tempo.Year, tempo.Month, tempo.Day, (tempo.Hour / 4) * 4, 0, 0),
            "D1" => new DateTime(tempo.Year, tempo.Month, tempo.Day, 0, 0, 0),
            _ => new DateTime(tempo.Year, tempo.Month, tempo.Day, tempo.Hour, (tempo.Minute / 5) * 5, 0) // Fallback para M5
        };
    }
}
