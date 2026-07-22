namespace Financial.Robot.Domain.Primitivos;

/// <summary>
/// Interface base para todos os eventos de domínio.
/// Eventos representam fatos que ocorreram no domínio (tempo passado).
/// </summary>
public interface IDomainEvent
{
    /// <summary>Momento em que o evento ocorreu.</summary>
    DateTime OcorridoEm { get; }
}
