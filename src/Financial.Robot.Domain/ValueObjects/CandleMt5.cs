namespace Financial.Robot.Domain.ValueObjects;

/// <summary>Candle histórico retornado pelo MT5 via CopyRates.</summary>
public sealed record CandleMt5(
    DateTime Tempo,
    double Abertura,
    double Maximo,
    double Minimo,
    double Fechamento,
    long Volume
);
