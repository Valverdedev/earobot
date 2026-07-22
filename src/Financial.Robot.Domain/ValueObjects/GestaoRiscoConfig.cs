namespace Financial.Robot.Domain.ValueObjects;

public record GestaoRiscoConfig(
    string ModoLote,
    double? LoteFixo,
    double? PercentualConta,
    int MaxOperacoesSimultaneas,
    double DrawdownDiarioMaximoPercent,
    JanelaHorarioConfig? JanelaHorarioPermitido
);
