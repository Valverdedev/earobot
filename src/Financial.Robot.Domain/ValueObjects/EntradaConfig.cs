namespace Financial.Robot.Domain.ValueObjects;

public record EntradaConfig(
    double? ComprarAcimaDe,
    double? ComprarAbaixoDe,
    double? VenderAcimaDe,
    double? VenderAbaixoDe
);
