namespace Financial.Robot.Domain.Excecoes;

/// <summary>
/// Exceção base para todas as violações de regras de negócio do domínio.
/// Toda exceção de domínio deve herdar desta classe.
/// </summary>
public class DomainException : Exception
{
    /// <summary>Inicializa a exceção com uma mensagem de erro.</summary>
    public DomainException(string mensagem) : base(mensagem) { }

    /// <summary>Inicializa a exceção com mensagem e exceção interna.</summary>
    public DomainException(string mensagem, Exception excecaoInterna)
        : base(mensagem, excecaoInterna) { }
}
