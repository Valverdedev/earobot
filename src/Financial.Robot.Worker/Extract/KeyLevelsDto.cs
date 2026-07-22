namespace Financial.Robot.Worker.Extract;

public sealed record KeyLevelsDto(
    IReadOnlyList<double> Supports,
    IReadOnlyList<double> Resistances,
    double? SessionHigh,
    double? SessionLow);
