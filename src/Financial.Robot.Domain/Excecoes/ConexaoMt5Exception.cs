using Financial.Robot.Domain.Excecoes;

namespace Financial.Robot.Domain.Excecoes;

/// <summary>
/// Exceção lançada quando a conexão com o terminal MetaTrader 5 falha.
/// </summary>
public sealed class ConexaoMt5Exception : DomainException
{
    /// <summary>Inicializa a exceção com mensagem de falha de conexão.</summary>
    public ConexaoMt5Exception(string mensagem) : base(mensagem) { }

    /// <summary>Inicializa com mensagem e exceção interna.</summary>
    public ConexaoMt5Exception(string mensagem, Exception excecaoInterna)
        : base(mensagem, excecaoInterna) { }
}
