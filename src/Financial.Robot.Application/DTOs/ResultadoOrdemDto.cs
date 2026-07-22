namespace Financial.Robot.Application.DTOs;

/// <summary>DTO com o resultado de uma ordem enviada ao MT5.</summary>
public sealed record ResultadoOrdemDto(
    bool Sucesso,
    ulong Ticket,
    double PrecoExecutado,
    double VolumeExecutado,
    int CodigoRetorno,
    string? MensagemErro = null);
