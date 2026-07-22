using Financial.Robot.Domain.Excecoes;

namespace Financial.Robot.Domain.ValueObjects;

/// <summary>
/// Value Object que representa um preço de mercado.
/// Imutável e auto-validante.
/// </summary>
public sealed record PrecoMercado
{
    /// <summary>Valor do preço (deve ser positivo).</summary>
    public double Valor { get; }

    /// <summary>
    /// Inicializa o preço validando que é maior que zero.
    /// </summary>
    public PrecoMercado(double valor)
    {
        if (valor <= 0)
            throw new DomainException($"Preço inválido: {valor}. Deve ser maior que zero.");

        Valor = valor;
    }

    /// <summary>Retorna representação em string com 5 casas decimais (padrão Forex).</summary>
    public override string ToString() => Valor.ToString("F5");

    /// <summary>Conversão implícita para double.</summary>
    public static implicit operator double(PrecoMercado preco) => preco.Valor;
}
