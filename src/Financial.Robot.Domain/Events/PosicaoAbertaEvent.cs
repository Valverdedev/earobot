using Financial.Robot.Domain.Primitivos;

namespace Financial.Robot.Domain.Events;

/// <summary>
/// Evento publicado quando uma posição a mercado é aberta com sucesso no MT5.
/// </summary>
public sealed record PosicaoAbertaEvent(
    ulong Ticket,
    string Simbolo,
    string TipoOrdem,
    double Volume,
    double PrecoAbertura,
    double StopLoss,
    double TakeProfit,
    DateTime OcorridoEm) : IDomainEvent;
