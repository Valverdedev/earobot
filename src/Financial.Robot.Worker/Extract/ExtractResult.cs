using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Worker.Extract;

public sealed record ExtractResult(
    DateTime ExtractedAtUtc,
    string TerminalId,
    string Host,
    int Port,
    string Kind,
    string? Symbol,
    string? Timeframe,
    int? Count,
    InfoContaMt5? Account,
    TickMt5? Tick,
    SymbolMetadataDto? SymbolMetadata,
    IReadOnlyList<CandleMt5>? Candles,
    IReadOnlyDictionary<string, IReadOnlyList<CandleMt5>>? Timeframes,
    IReadOnlyList<DetalhesPosicaoMt5>? OpenPositions,
    IReadOnlyList<DealMt5>? Deals,
    AnalysisContextDto? AnalysisContext);
