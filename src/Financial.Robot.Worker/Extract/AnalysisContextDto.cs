namespace Financial.Robot.Worker.Extract;

public sealed record AnalysisContextDto(
    string TrendSummary,
    string VolatilityRegime,
    KeyLevelsDto KeyLevels,
    DataQualityDto DataQuality,
    IReadOnlyDictionary<string, TimeframeAnalysisDto> Timeframes,
    IReadOnlyList<string> RiskFlags);
