using Financial.Robot.Domain.Primitivos;

namespace Financial.Robot.Domain.Events;

/// <summary>
/// Evento publicado quando o Stop Loss ou Take Profit de uma posição é modificado.
/// </summary>
public sealed record PosicaoModificadaEvent(
    ulong Ticket,
    string Simbolo,
    double StopLossAnterior,
    double StopLossNovo,
    double TakeProfitAnterior,
    double TakeProfitNovo,
    DateTime OcorridoEm) : IDomainEvent;
