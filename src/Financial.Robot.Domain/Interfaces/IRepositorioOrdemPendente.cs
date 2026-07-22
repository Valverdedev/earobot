using Financial.Robot.Domain.Aggregates.OrdemPendente;

namespace Financial.Robot.Domain.Interfaces;

/// <summary>
/// Repositório de ordens pendentes.
/// Interface definida no Domain — implementada na Infrastructure.
/// </summary>
public interface IRepositorioOrdemPendente
{
    /// <summary>Adiciona uma nova ordem pendente ao repositório.</summary>
    Task AdicionarAsync(OrdemPendente ordemPendente, CancellationToken ct = default);

    /// <summary>Obtém uma ordem pendente pelo ticket. Retorna null se não encontrada.</summary>
    Task<OrdemPendente?> ObterPorTicketAsync(ulong ticket, CancellationToken ct = default);

    /// <summary>Atualiza os dados de uma ordem pendente existente.</summary>
    Task AtualizarAsync(OrdemPendente ordemPendente, CancellationToken ct = default);
}
