using Financial.Robot.Domain.Primitivos;

namespace Financial.Robot.Domain.Events;

/// <summary>
/// Evento publicado quando uma ordem pendente é cancelada no MT5.
/// </summary>
public sealed record OrdemPendenteCanceladaEvent(
    ulong Ticket,
    string Simbolo,
    DateTime OcorridoEm) : IDomainEvent;
