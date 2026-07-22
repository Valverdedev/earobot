namespace Financial.Robot.Domain.Primitivos;

/// <summary>
/// Classe base para todas as entidades do domínio.
/// Igualdade baseada em identidade (Id), não em atributos.
/// </summary>
public abstract class Entidade<TId> : IEquatable<Entidade<TId>>
    where TId : notnull
{
    /// <summary>Identificador único da entidade.</summary>
    public TId Id { get; protected set; } = default!;

    /// <summary>Inicializa a entidade com um identificador.</summary>
    protected Entidade(TId id) => Id = id;

    /// <summary>Construtor sem parâmetros para compatibilidade com ORMs.</summary>
    protected Entidade() { }

    /// <inheritdoc/>
    public bool Equals(Entidade<TId>? other) =>
        other is not null && Id.Equals(other.Id);

    /// <inheritdoc/>
    public override bool Equals(object? obj) =>
        obj is Entidade<TId> entidade && Equals(entidade);

    /// <inheritdoc/>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>Operador de igualdade por identidade.</summary>
    public static bool operator ==(Entidade<TId>? esquerda, Entidade<TId>? direita) =>
        Equals(esquerda, direita);

    /// <summary>Operador de desigualdade por identidade.</summary>
    public static bool operator !=(Entidade<TId>? esquerda, Entidade<TId>? direita) =>
        !Equals(esquerda, direita);
}
