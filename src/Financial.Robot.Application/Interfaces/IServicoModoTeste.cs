using Financial.Robot.Application.DTOs;

namespace Financial.Robot.Application.Interfaces;

/// <summary>
/// Contrato do serviço que orquestra a sequência de 14 passos do Modo Teste MT5.
/// </summary>
public interface IServicoModoTeste
{
    /// <summary>
    /// Executa a sequência completa do modo teste e retorna o resumo com status de cada passo.
    /// </summary>
    Task<ResumoTesteDto> ExecutarAsync(CancellationToken ct = default);
}
