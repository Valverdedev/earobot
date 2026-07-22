namespace Financial.Robot.Application.DTOs;

/// <summary>DTO com status e resultado de cada passo da sequência de teste.</summary>
public sealed record ResultadoPassoDto(
    int NumeroPasso,
    string NomePasso,
    bool Sucesso,
    string? Detalhe,
    DateTime ExecutadoEm,
    TimeSpan Duracao);
