namespace Financial.Robot.Domain.ValueObjects;

public record EstrategiaConfig(
    string Id,
    string Nome,
    long MagicNumber,
    bool Ativa,
    bool Comprar,
    bool Vender,
    EntradaConfig? Entrada,
    SaidaConfig? Saida,
    GestaoRiscoConfig? GestaoDeRisco,
    JanelaHorarioConfig? JanelaHorarioPermitido,
    List<IndicadorConfig>? Indicadores,
    Dictionary<string, object>? Parametros
);
