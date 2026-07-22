using Financial.Robot.Domain.Excecoes;

namespace Financial.Robot.Domain.ValueObjects;

/// <summary>
/// Value Object que representa o símbolo de um ativo financeiro (ex: EURUSD, WINQ25).
/// Imutável e auto-validante.
/// </summary>
public sealed record Simbolo
{
    /// <summary>Valor do símbolo em letras maiúsculas.</summary>
    public string Valor { get; }

    /// <summary>
    /// Inicializa o símbolo validando que não é vazio e normalizando para maiúsculas.
    /// </summary>
    public Simbolo(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw new DomainException("O símbolo do ativo não pode ser vazio.");

        Valor = valor.Trim().ToUpperInvariant();
    }

    /// <summary>Retorna a representação em string do símbolo.</summary>
    public override string ToString() => Valor;

    /// <summary>Conversão implícita para string.</summary>
    public static implicit operator string(Simbolo simbolo) => simbolo.Valor;
}
