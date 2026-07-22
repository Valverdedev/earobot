namespace Financial.Robot.Application.Interfaces;

/// <summary>Resultado de uma operação de execução de ordem.</summary>
public sealed record ResultadoOrdem(
    bool Sucesso,
    ulong? Ticket,
    string Motivo
)
{
    public static ResultadoOrdem Ok(ulong ticket)     => new(true,  ticket, "Sucesso");
    public static ResultadoOrdem Falha(string motivo) => new(false, null,   motivo);
}
