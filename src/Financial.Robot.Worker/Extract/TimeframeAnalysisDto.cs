namespace Financial.Robot.Worker.Extract;

public sealed record TimeframeAnalysisDto(
    string Timeframe,
    int CandleCount,
    DateTime? FirstCandleTime,
    DateTime? LastCandleTime,
    double? LastClose,
    double? Ema9,
    double? Ema21,
    double? Ema50,
    double? Rsi14,
    double? Atr14,
    double? Vwap,
    double? RangeHigh,
    double? RangeLow,
    string Trend,
    string VolatilityRegime);
