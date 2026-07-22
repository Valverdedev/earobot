namespace Financial.Robot.Worker.Extract;

public sealed record DataQualityDto(
    DateTime? TickTime,
    DateTime? TickTimeUtc,
    double? TickAgeSeconds,
    DateTime? LastCandleTime,
    DateTime? LastCandleTimeUtc,
    double? LastCandleAgeSeconds,
    bool CandlesOrdered,
    int DuplicateCandles,
    int GapCount,
    IReadOnlyList<string> Warnings);
