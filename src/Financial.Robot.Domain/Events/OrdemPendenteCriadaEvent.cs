using Financial.Robot.Domain.Primitivos;

namespace Financial.Robot.Domain.Events;

/// <summary>
/// Evento publicado quando uma ordem pendente é criada no MT5.
/// </summary>
public sealed record OrdemPendenteCriadaEvent(
    ulong Ticket,
    string Simbolo,
    string TipoOrdem,
    double Volume,
    double PrecoOrdem,
    double StopLoss,
    double TakeProfit,
    DateTime OcorridoEm) : IDomainEvent;
