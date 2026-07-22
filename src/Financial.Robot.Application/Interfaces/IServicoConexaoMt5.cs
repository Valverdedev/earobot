using Financial.Robot.Application.DTOs;

namespace Financial.Robot.Application.Interfaces;

/// <summary>
/// Contrato do serviço de conexão com o MetaTrader 5.
/// Gerencia o ciclo de vida da conexão e expõe info da conta.
/// </summary>
public interface IServicoConexaoMt5
{
    /// <summary>Conecta ao terminal MT5 usando as configurações fornecidas.</summary>
    Task<InfoContaDto> ConectarAsync(
        string host,
        int porta,
        int timeoutSegundos,
        CancellationToken ct = default);

    /// <summary>Desconecta do terminal MT5.</summary>
    Task DesconectarAsync(CancellationToken ct = default);

    /// <summary>Indica se a conexão com o MT5 está ativa.</summary>
    bool EstaConectado { get; }
}
