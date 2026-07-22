namespace Financial.Robot.Domain.ValueObjects;

public record IndicadorConfig(
    string Nome,
    string Timeframe,
    Dictionary<string, object>? Parametros
);
