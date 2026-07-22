using Financial.Robot.Domain.Primitivos;

namespace Financial.Robot.Domain.Entities;

/// <summary>
/// Entidade que representa as informações da conta MT5 conectada.
/// Não é um Aggregate Root — não tem repositório próprio.
/// </summary>
public sealed class InfoConta : Entidade<long>
{
    /// <summary>Login numérico da conta.</summary>
    public long Login { get; private set; }

    /// <summary>Nome do servidor (corretora).</summary>
    public string Servidor { get; private set; } = string.Empty;

    /// <summary>Nome do titular da conta.</summary>
    public string NomeTitular { get; private set; } = string.Empty;

    /// <summary>Saldo disponível na conta.</summary>
    public double Saldo { get; private set; }

    /// <summary>Equidade atual (saldo + resultado posições abertas).</summary>
    public double Equidade { get; private set; }

    /// <summary>Margem livre disponível.</summary>
    public double MargemLivre { get; private set; }

    /// <summary>Moeda da conta (ex: USD, BRL).</summary>
    public string Moeda { get; private set; } = string.Empty;

    private InfoConta() { }

    /// <summary>
    /// Cria uma instância de InfoConta a partir dos dados retornados pelo MT5.
    /// </summary>
    public static InfoConta Criar(
        long login,
        string servidor,
        string nomeTitular,
        double saldo,
        double equidade,
        double margemLivre,
        string moeda) =>
        new()
        {
            Id = login,
            Login = login,
            Servidor = servidor,
            NomeTitular = nomeTitular,
            Saldo = saldo,
            Equidade = equidade,
            MargemLivre = margemLivre,
            Moeda = moeda
        };
}
