using Financial.Robot.Domain.Aggregates.OrdemPendente;
using Financial.Robot.Domain.Interfaces;

namespace Financial.Robot.Infrastructure.Repositorios;

/// <summary>
/// Repositório de ordens pendentes em memória.
/// Implementação MVP — sem persistência em banco de dados.
/// </summary>
public sealed class RepositorioOrdemPendenteEmMemoria : IRepositorioOrdemPendente
{
    private readonly Dictionary<ulong, OrdemPendente> _ordens = [];

    /// <inheritdoc/>
    public Task AdicionarAsync(OrdemPendente ordemPendente, CancellationToken ct = default)
    {
        _ordens[ordemPendente.Id] = ordemPendente;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<OrdemPendente?> ObterPorTicketAsync(ulong ticket, CancellationToken ct = default)
    {
        _ordens.TryGetValue(ticket, out var ordem);
        return Task.FromResult(ordem);
    }

    /// <inheritdoc/>
    public Task AtualizarAsync(OrdemPendente ordemPendente, CancellationToken ct = default)
    {
        _ordens[ordemPendente.Id] = ordemPendente;
        return Task.CompletedTask;
    }
}
