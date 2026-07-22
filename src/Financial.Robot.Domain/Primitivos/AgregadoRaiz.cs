namespace Financial.Robot.Domain.Primitivos;

/// <summary>
/// Classe base para raízes de agregado.
/// Gerencia e publica eventos de domínio.
/// </summary>
public abstract class AgregadoRaiz<TId> : Entidade<TId>
    where TId : notnull
{
    private readonly List<IDomainEvent> _eventosDominio = [];

    /// <summary>Lista somente-leitura de eventos de domínio pendentes de publicação.</summary>
    public IReadOnlyList<IDomainEvent> EventosDominio =>
        _eventosDominio.AsReadOnly();

    /// <summary>Registra um evento de domínio a ser publicado.</summary>
    protected void PublicarEvento(IDomainEvent evento) =>
        _eventosDominio.Add(evento);

    /// <summary>Limpa os eventos de domínio após publicação.</summary>
    public void LimparEventos() => _eventosDominio.Clear();

    /// <summary>Inicializa o agregado com um identificador.</summary>
    protected AgregadoRaiz(TId id) : base(id) { }

    /// <summary>Construtor sem parâmetros para compatibilidade com ORMs.</summary>
    protected AgregadoRaiz() { }
}
