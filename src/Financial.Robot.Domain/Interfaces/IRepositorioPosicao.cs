using Financial.Robot.Domain.Aggregates.Posicao;

namespace Financial.Robot.Domain.Interfaces;

/// <summary>
/// Repositório de posições a mercado.
/// Interface definida no Domain — implementada na Infrastructure.
/// </summary>
public interface IRepositorioPosicao
{
    /// <summary>Adiciona uma nova posição ao repositório.</summary>
    Task AdicionarAsync(Posicao posicao, CancellationToken ct = default);

    /// <summary>Obtém uma posição pelo ticket do MT5. Retorna null se não encontrada.</summary>
    Task<Posicao?> ObterPorTicketAsync(ulong ticket, CancellationToken ct = default);

    /// <summary>Atualiza os dados de uma posição existente.</summary>
    Task AtualizarAsync(Posicao posicao, CancellationToken ct = default);
}
