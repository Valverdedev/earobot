using Financial.Robot.Domain.ValueObjects;

namespace Financial.Robot.Domain.Entities;

public record SymbolConfig(
    string SchemaVersion,
    string Symbol,
    string BrokerSymbol,
    string TerminalId,
    DateTime GeradoEm,
    string OrigemRelatorio,
    bool Operar,
    bool Comprar,
    bool Vender,
    EntradaConfig Entrada,
    SaidaConfig Saida,
    GestaoRiscoConfig GestaoDeRisco,
    List<IndicadorConfig>? Indicadores,
    List<IndicadorConfig>? IndicadoresMultiFonte,
    string ConfiancaSinal,
    string Perfil,
    double? DrawdownDiarioMaximoAgregadoPercent = null,
    List<EstrategiaConfig>? Estrategias = null
);
