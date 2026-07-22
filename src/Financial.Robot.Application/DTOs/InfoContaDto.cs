namespace Financial.Robot.Application.DTOs;

/// <summary>DTO com informações da conta MT5 para a camada de apresentação.</summary>
public sealed record InfoContaDto(
    long Login,
    string Servidor,
    string NomeTitular,
    double Saldo,
    double Equidade,
    double MargemLivre,
    string Moeda);
