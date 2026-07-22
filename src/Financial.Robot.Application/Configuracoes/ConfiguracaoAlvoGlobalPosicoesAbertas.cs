namespace Financial.Robot.Application.Configuracoes;

/// <summary>
/// Define a unidade de medida para os alvos de lucro e stop de perda global.
/// </summary>
public enum UnidadeAlvoGlobal
{
    /// <summary>Percentual sobre o saldo da conta (ex: 1.0 = 1%).</summary>
    Percentual,

    /// <summary>Valor absoluto em moeda (ex: 200.0 = R$200 ou US$200).</summary>
    Absoluto
}

/// <summary>
/// Configuração de proteção global de posições abertas por terminal.
/// Ao cruzar a meta de lucro ou o stop de perda configurados, fecha todas as
/// posições abertas naquele terminal. Lida via IOptionsMonitor para permitir
/// alterações em runtime sem reiniciar o processo.
/// </summary>
public sealed class ConfiguracaoAlvoGlobalPosicoesAbertas
{
    /// <summary>Liga/desliga o mecanismo para este terminal. Default false.</summary>
    public bool Ativo { get; set; } = false;

    /// <summary>Identificador do terminal de execução (ex: "activtraders", "genial").</summary>
    public string TerminalId { get; set; } = string.Empty;

    /// <summary>
    /// Unidade dos alvos: <see cref="UnidadeAlvoGlobal.Percentual"/> (% sobre saldo)
    /// ou <see cref="UnidadeAlvoGlobal.Absoluto"/> (valor fixo em moeda).
    /// </summary>
    public UnidadeAlvoGlobal UnidadeAlvo { get; set; } = UnidadeAlvoGlobal.Percentual;

    // ── Modo Percentual ──────────────────────────────────────────────────────

    /// <summary>
    /// [Percentual] Fecha tudo quando lucro flutuante >= valor (%). Null = desativado.
    /// </summary>
    public double? MetaLucroGlobalPercent { get; set; }

    /// <summary>
    /// [Percentual] Fecha tudo quando perda flutuante >= valor (%). Null = desativado.
    /// </summary>
    public double? StopPerdaGlobalPercent { get; set; }

    // ── Modo Absoluto ────────────────────────────────────────────────────────

    /// <summary>
    /// [Absoluto] Fecha tudo quando lucro flutuante >= valor em moeda. Null = desativado.
    /// </summary>
    public double? MetaLucroGlobalAbsoluto { get; set; }

    /// <summary>
    /// [Absoluto] Fecha tudo quando perda flutuante >= valor em moeda. Null = desativado.
    /// </summary>
    public double? StopPerdaGlobalAbsoluto { get; set; }

    /// <summary>Cadência do ciclo de verificação em segundos. Mínimo efetivo: 5s.</summary>
    public int IntervaloVerificacaoSegundos { get; set; } = 10;
}
