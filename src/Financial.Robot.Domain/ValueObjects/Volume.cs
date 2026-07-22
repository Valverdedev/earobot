using Financial.Robot.Domain.Excecoes;

namespace Financial.Robot.Domain.ValueObjects;

/// <summary>
/// Value Object que representa o volume de uma ordem (lotes).
/// Imutável e auto-validante.
/// </summary>
public sealed record Volume
{
    /// <summary>Quantidade em lotes (ex: 0.01, 0.1, 1.0).</summary>
    public double Valor { get; }

    /// <summary>
    /// Inicializa o volume validando que é maior que zero.
    /// </summary>
    public Volume(double valor)
    {
        if (valor <= 0)
            throw new DomainException($"Volume inválido: {valor}. Deve ser maior que zero.");

        Valor = valor;
    }

    /// <summary>Retorna representação em string com 2 casas decimais.</summary>
    public override string ToString() => Valor.ToString("F2");

    /// <summary>Conversão implícita para double.</summary>
    public static implicit operator double(Volume volume) => volume.Valor;
}
