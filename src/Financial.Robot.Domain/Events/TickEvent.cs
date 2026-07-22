namespace Financial.Robot.Domain.Events;

public record TickEvent(
    string TerminalId,
    string Symbol,
    double Bid,
    double Ask,
    DateTime Timestamp
);
