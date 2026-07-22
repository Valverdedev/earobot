using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Worker.Extract;

public static class AnalysisContextBuilder
{
    public static AnalysisContextDto Build(
        TickMt5? tick,
        SymbolMetadataDto? metadata,
        IReadOnlyDictionary<string, IReadOnlyList<CandleMt5>> candlesByTimeframe,
        IReadOnlyList<DetalhesPosicaoMt5>? positions,
        DateTime extractedAtUtc)
    {
        var timeframeAnalysis = candlesByTimeframe.ToDictionary(
            pair => pair.Key,
            pair => AnalyzeTimeframe(pair.Key, pair.Value),
            StringComparer.OrdinalIgnoreCase);

        var primaryCandles = ResolvePrimaryCandles(candlesByTimeframe);
        var dataQuality = BuildDataQuality(tick, primaryCandles, extractedAtUtc);
        var keyLevels = BuildKeyLevels(primaryCandles);
        var riskFlags = BuildRiskFlags(tick, metadata, primaryCandles, positions, dataQuality);

        return new AnalysisContextDto(
            BuildTrendSummary(timeframeAnalysis),
            BuildVolatilityRegime(timeframeAnalysis),
            keyLevels,
            dataQuality,
            timeframeAnalysis,
            riskFlags);
    }

    private static TimeframeAnalysisDto AnalyzeTimeframe(string timeframe, IReadOnlyList<CandleMt5> sourceCandles)
    {
        var candles = sourceCandles.OrderBy(c => c.Tempo).ToList();
        var closes = candles.Select(c => c.Fechamento).ToList();
        var lastClose = closes.Count > 0 ? closes[^1] : (double?)null;
        var ema9 = CalculateEma(closes, 9);
        var ema21 = CalculateEma(closes, 21);
        var ema50 = CalculateEma(closes, 50);
        var atr14 = CalculateAtr(candles, 14);

        return new TimeframeAnalysisDto(
            timeframe,
            candles.Count,
            candles.FirstOrDefault()?.Tempo,
            candles.LastOrDefault()?.Tempo,
            lastClose,
            ema9,
            ema21,
            ema50,
            CalculateRsi(closes, 14),
            atr14,
            CalculateVwap(candles),
            candles.Count > 0 ? candles.Max(c => c.Maximo) : null,
            candles.Count > 0 ? candles.Min(c => c.Minimo) : null,
            ClassifyTrend(lastClose, ema9, ema21, ema50),
            ClassifyVolatility(lastClose, atr14));
    }

    private static IReadOnlyList<CandleMt5> ResolvePrimaryCandles(
        IReadOnlyDictionary<string, IReadOnlyList<CandleMt5>> candlesByTimeframe)
    {
        if (candlesByTimeframe.TryGetValue("M1", out var m1) && m1.Count > 0)
            return m1;

        return candlesByTimeframe.Values.FirstOrDefault(c => c.Count > 0) ?? [];
    }

    private static DataQualityDto BuildDataQuality(
        TickMt5? tick,
        IReadOnlyList<CandleMt5> sourceCandles,
        DateTime extractedAtUtc)
    {
        var candles = sourceCandles.OrderBy(c => c.Tempo).ToList();
        var warnings = new List<string>();
        DateTime? tickTimeUtc = tick is null ? null : NormalizeMt5TimestampToUtc(tick.Tempo);
        double? tickAge = tickTimeUtc is null ? null : Math.Max(0, (extractedAtUtc - tickTimeUtc.Value).TotalSeconds);
        var lastCandleTime = candles.LastOrDefault()?.Tempo;
        DateTime? lastCandleTimeUtc = lastCandleTime is null ? null : NormalizeMt5TimestampToUtc(lastCandleTime.Value);
        double? lastCandleAge = lastCandleTimeUtc is null ? null : Math.Max(0, (extractedAtUtc - lastCandleTimeUtc.Value).TotalSeconds);
        var ordered = sourceCandles.SequenceEqual(candles);
        var duplicates = candles.Count - candles.Select(c => c.Tempo).Distinct().Count();
        var gaps = CountGaps(candles);

        if (tickAge is > 120)
            warnings.Add($"Tick com idade alta: {tickAge:F0}s.");

        if (lastCandleAge is > 300)
            warnings.Add($"Último candle com idade alta: {lastCandleAge:F0}s.");

        if (!ordered)
            warnings.Add("Candles fora de ordem cronológica.");

        if (duplicates > 0)
            warnings.Add($"Candles duplicados por timestamp: {duplicates}.");

        if (gaps > 0)
            warnings.Add($"Possíveis gaps entre candles: {gaps}.");

        return new DataQualityDto(
            tick?.Tempo,
            tickTimeUtc,
            tickAge,
            lastCandleTime,
            lastCandleTimeUtc,
            lastCandleAge,
            ordered,
            duplicates,
            gaps,
            warnings);
    }

    private static DateTime NormalizeMt5TimestampToUtc(DateTime timestamp)
    {
        return timestamp.Kind switch
        {
            DateTimeKind.Utc => timestamp,
            DateTimeKind.Local => timestamp.ToUniversalTime(),
            _ => DateTime.SpecifyKind(timestamp, DateTimeKind.Local).ToUniversalTime()
        };
    }

    private static KeyLevelsDto BuildKeyLevels(IReadOnlyList<CandleMt5> sourceCandles)
    {
        var candles = sourceCandles.OrderBy(c => c.Tempo).ToList();
        if (candles.Count == 0)
            return new KeyLevelsDto([], [], null, null);

        var lastClose = candles[^1].Fechamento;
        var recent = candles.TakeLast(Math.Min(120, candles.Count)).ToList();
        var supports = new List<double>();
        var resistances = new List<double>();

        for (var i = 2; i < recent.Count - 2; i++)
        {
            var candle = recent[i];
            if (candle.Minimo <= recent[i - 1].Minimo
                && candle.Minimo <= recent[i - 2].Minimo
                && candle.Minimo <= recent[i + 1].Minimo
                && candle.Minimo <= recent[i + 2].Minimo
                && candle.Minimo < lastClose)
            {
                supports.Add(candle.Minimo);
            }

            if (candle.Maximo >= recent[i - 1].Maximo
                && candle.Maximo >= recent[i - 2].Maximo
                && candle.Maximo >= recent[i + 1].Maximo
                && candle.Maximo >= recent[i + 2].Maximo
                && candle.Maximo > lastClose)
            {
                resistances.Add(candle.Maximo);
            }
        }

        return new KeyLevelsDto(
            supports.Distinct().OrderByDescending(level => Math.Abs(level - lastClose)).TakeLast(5).OrderByDescending(level => level).ToList(),
            resistances.Distinct().OrderBy(level => Math.Abs(level - lastClose)).Take(5).OrderBy(level => level).ToList(),
            recent.Max(c => c.Maximo),
            recent.Min(c => c.Minimo));
    }

    private static IReadOnlyList<string> BuildRiskFlags(
        TickMt5? tick,
        SymbolMetadataDto? metadata,
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<DetalhesPosicaoMt5>? positions,
        DataQualityDto dataQuality)
    {
        var flags = new List<string>();

        if (tick is not null && tick.Ask <= tick.Bid)
            flags.Add("Spread inválido: ask <= bid.");

        if (tick is not null && metadata is not null && metadata.PointSize > 0)
        {
            var spreadPoints = (tick.Ask - tick.Bid) / metadata.PointSize;
            if (spreadPoints > 4)
                flags.Add($"Spread atual elevado: {spreadPoints:F1} pontos mínimos.");
        }

        if (positions is { Count: > 0 })
            flags.Add($"Há {positions.Count} posição(ões) aberta(s) no símbolo.");

        flags.AddRange(dataQuality.Warnings);

        if (candles.Count < 50)
            flags.Add("Amostra curta de candles; indicadores longos podem ser pouco confiáveis.");

        return flags;
    }

    private static string BuildTrendSummary(IReadOnlyDictionary<string, TimeframeAnalysisDto> analysis)
    {
        if (analysis.Count == 0)
            return "indisponivel";

        var bullish = analysis.Values.Count(a => a.Trend == "alta");
        var bearish = analysis.Values.Count(a => a.Trend == "baixa");

        if (bullish > bearish)
            return "predominio_alta";

        if (bearish > bullish)
            return "predominio_baixa";

        return "misto_lateral";
    }

    private static string BuildVolatilityRegime(IReadOnlyDictionary<string, TimeframeAnalysisDto> analysis)
    {
        var regimes = analysis.Values
            .Select(a => a.VolatilityRegime)
            .Where(r => r != "indisponivel")
            .ToList();

        if (regimes.Count == 0)
            return "indisponivel";

        if (regimes.Count(r => r == "alta") >= regimes.Count / 2.0)
            return "alta";

        if (regimes.Count(r => r == "baixa") > regimes.Count / 2.0)
            return "baixa";

        return "normal";
    }

    private static double? CalculateEma(IReadOnlyList<double> values, int period)
    {
        if (values.Count < period)
            return null;

        var multiplier = 2.0 / (period + 1);
        var ema = values.Take(period).Average();

        for (var i = period; i < values.Count; i++)
            ema = ((values[i] - ema) * multiplier) + ema;

        return ema;
    }

    private static double? CalculateRsi(IReadOnlyList<double> closes, int period)
    {
        if (closes.Count <= period)
            return null;

        double gains = 0;
        double losses = 0;

        for (var i = closes.Count - period; i < closes.Count; i++)
        {
            var change = closes[i] - closes[i - 1];
            if (change >= 0)
                gains += change;
            else
                losses -= change;
        }

        if (losses == 0)
            return 100;

        var relativeStrength = gains / losses;
        return 100 - (100 / (1 + relativeStrength));
    }

    private static double? CalculateAtr(IReadOnlyList<CandleMt5> candles, int period)
    {
        if (candles.Count <= period)
            return null;

        var ordered = candles.OrderBy(c => c.Tempo).ToList();
        var trueRanges = new List<double>();

        for (var i = 1; i < ordered.Count; i++)
        {
            var current = ordered[i];
            var previous = ordered[i - 1];
            var range = Math.Max(
                current.Maximo - current.Minimo,
                Math.Max(
                    Math.Abs(current.Maximo - previous.Fechamento),
                    Math.Abs(current.Minimo - previous.Fechamento)));

            trueRanges.Add(range);
        }

        return trueRanges.TakeLast(period).Average();
    }

    private static double? CalculateVwap(IReadOnlyList<CandleMt5> candles)
    {
        var validCandles = candles.Where(c => c.Volume > 0).ToList();
        if (validCandles.Count == 0)
            return null;

        var totalVolume = validCandles.Sum(c => (double)c.Volume);
        if (totalVolume <= 0)
            return null;

        var total = validCandles.Sum(c => ((c.Maximo + c.Minimo + c.Fechamento) / 3.0) * c.Volume);
        return total / totalVolume;
    }

    private static string ClassifyTrend(double? lastClose, double? ema9, double? ema21, double? ema50)
    {
        if (!lastClose.HasValue || !ema9.HasValue || !ema21.HasValue)
            return "indisponivel";

        if (lastClose > ema9 && ema9 > ema21 && (!ema50.HasValue || ema21 > ema50))
            return "alta";

        if (lastClose < ema9 && ema9 < ema21 && (!ema50.HasValue || ema21 < ema50))
            return "baixa";

        return "lateral_misto";
    }

    private static string ClassifyVolatility(double? lastClose, double? atr)
    {
        if (!lastClose.HasValue || !atr.HasValue || lastClose.Value <= 0)
            return "indisponivel";

        var atrPercent = atr.Value / lastClose.Value * 100.0;
        return atrPercent switch
        {
            >= 0.20 => "alta",
            <= 0.05 => "baixa",
            _ => "normal"
        };
    }

    private static int CountGaps(IReadOnlyList<CandleMt5> candles)
    {
        if (candles.Count < 3)
            return 0;

        var ordered = candles.OrderBy(c => c.Tempo).ToList();
        var intervals = new List<double>();

        for (var i = 1; i < ordered.Count; i++)
        {
            var minutes = (ordered[i].Tempo - ordered[i - 1].Tempo).TotalMinutes;
            if (minutes > 0)
                intervals.Add(minutes);
        }

        if (intervals.Count == 0)
            return 0;

        var expected = intervals
            .GroupBy(i => Math.Round(i, 2))
            .OrderByDescending(g => g.Count())
            .First()
            .Key;

        return intervals.Count(i => i > expected * 1.5);
    }
}
