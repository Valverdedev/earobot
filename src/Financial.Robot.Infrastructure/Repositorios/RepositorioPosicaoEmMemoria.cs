using Financial.Robot.Domain.Aggregates.Posicao;
using Financial.Robot.Domain.Interfaces;

namespace Financial.Robot.Infrastructure.Repositorios;

/// <summary>
/// Repositório de posições em memória.
/// Implementação MVP — sem persistência em banco de dados.
/// Suficiente para a sequência de teste que roda uma única vez.
/// </summary>
public sealed class RepositorioPosicaoEmMemoria : IRepositorioPosicao
{
    private readonly Dictionary<ulong, Posicao> _posicoes = [];

    /// <inheritdoc/>
    public Task AdicionarAsync(Posicao posicao, CancellationToken ct = default)
    {
        _posicoes[posicao.Id] = posicao;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<Posicao?> ObterPorTicketAsync(ulong ticket, CancellationToken ct = default)
    {
        _posicoes.TryGetValue(ticket, out var posicao);
        return Task.FromResult(posicao);
    }

    /// <inheritdoc/>
    public Task AtualizarAsync(Posicao posicao, CancellationToken ct = default)
    {
        _posicoes[posicao.Id] = posicao;
        return Task.CompletedTask;
    }
}
