using Financial.Robot.Domain.Excecoes;

namespace Financial.Robot.Domain.ValueObjects;

/// <summary>
/// Value Object que representa uma distância em pips.
/// Usado para definir Stop Loss, Take Profit e distância de ordens pendentes.
/// Imutável e auto-validante.
/// </summary>
public sealed record Pips
{
    /// <summary>Quantidade de pips (sempre positivo).</summary>
    public int Valor { get; }

    /// <summary>
    /// Inicializa os pips validando que são positivos.
    /// </summary>
    public Pips(int valor)
    {
        if (valor <= 0)
            throw new DomainException($"Pips inválido: {valor}. Deve ser maior que zero.");

        Valor = valor;
    }

    /// <summary>Retorna representação em string.</summary>
    public override string ToString() => $"{Valor} pips";

    /// <summary>Conversão implícita para int.</summary>
    public static implicit operator int(Pips pips) => pips.Valor;
}
