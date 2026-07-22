using Financial.Robot.Domain.Excecoes;

namespace Financial.Robot.Domain.Excecoes;

/// <summary>
/// Exceção lançada quando uma ordem enviada ao MT5 é inválida ou rejeitada.
/// </summary>
public sealed class OrdemInvalidaException : DomainException
{
    /// <summary>Código de erro retornado pelo MT5, se disponível.</summary>
    public int? CodigoErroMt5 { get; }

    /// <summary>Inicializa com mensagem de ordem inválida.</summary>
    public OrdemInvalidaException(string mensagem) : base(mensagem) { }

    /// <summary>Inicializa com mensagem e código de erro do MT5.</summary>
    public OrdemInvalidaException(string mensagem, int codigoErroMt5)
        : base(mensagem)
    {
        CodigoErroMt5 = codigoErroMt5;
    }
}
