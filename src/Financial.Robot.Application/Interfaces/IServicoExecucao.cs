using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Application.Interfaces;

/// <summary>
/// Contrato do serviço de execução de ordens no MT5.
/// Orientado a comando — não possui lógica de decisão.
/// </summary>
public interface IServicoExecucao
{
    /// <summary>Envia ordem a mercado (compra/venda) usando as configurações do robô.</summary>
    Task<ResultadoOrdem> AbrirPosicaoAsync(
        string terminalId,
        string simbolo,
        LadoOrdem lado,
        double volume,
        double? sl,
        double? tp,
        long magicNumber = 0,
        string comentario = "financial.robot",
        CancellationToken ct = default);

    /// <summary>Fecha uma posição pelo ticket.</summary>
    Task<ResultadoOrdem> FecharPosicaoAsync(
        string terminalId,
        ulong ticket,
        CancellationToken ct = default);

    /// <summary>Fecha parcialmente uma posição, reduzindo seu volume pelo valor informado.</summary>
    Task<ResultadoOrdem> FecharPosicaoParcialAsync(
        string terminalId,
        ulong ticket,
        double volume,
        CancellationToken ct = default);

    /// <summary>Modifica SL e TP de uma posição existente.</summary>
    Task<ResultadoOrdem> ModificarPosicaoAsync(
        string terminalId,
        ulong ticket,
        double? sl,
        double? tp,
        CancellationToken ct = default);
}
