using Financial.Robot.Application.DTOs;

namespace Financial.Robot.Application.DTOs;

/// <summary>
/// DTO com o resumo completo da execução do modo teste.
/// Serializado como JSON no log ao final da sequência.
/// </summary>
public sealed record ResumoTesteDto(
    bool Sucesso,
    int TotalPassos,
    int PassosConcluidos,
    int? PassoParada,
    string? MotivoParada,
    IReadOnlyList<ResultadoPassoDto> Passos,
    DateTime IniciadoEm,
    DateTime FinalizadoEm,
    TimeSpan DuracaoTotal);
