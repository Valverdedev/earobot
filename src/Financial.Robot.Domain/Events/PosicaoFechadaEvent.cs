using Financial.Robot.Domain.Primitivos;

namespace Financial.Robot.Domain.Events;

/// <summary>
/// Evento publicado quando uma posição é fechada no MT5.
/// </summary>
public sealed record PosicaoFechadaEvent(
    ulong Ticket,
    string Simbolo,
    double PrecoFechamento,
    double LucroPrejuizo,
    DateTime OcorridoEm) : IDomainEvent;
