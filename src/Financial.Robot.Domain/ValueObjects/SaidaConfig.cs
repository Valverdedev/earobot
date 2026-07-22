namespace Financial.Robot.Domain.ValueObjects;

public record SaidaConfig(
    double? EncerrarAtivaAcimaDe = null,
    double? EncerrarAtivaAbaixoDe = null,
    decimal? StopLossPips = null,
    decimal? TakeProfitPips = null,
    decimal? TrailingStopPips = null,
    UnidadeDistancia? UnidadeDistancia = null,
    decimal? StopLossAtrMultiplo = null,
    decimal? TakeProfitAtrMultiplo = null,
    decimal? SlMinimoSobreSpread = null,
    bool ApplyToOpenPositions = false,
    decimal? BreakevenGatilhoAtrMultiplo = null,
    decimal? BreakevenBufferPips = null,
    decimal? TrailingStopAtrMultiplo = null,
    int? CooldownAposFechamentoSegundos = null,
    IReadOnlyList<SaidaParcialConfig>? SaidasParciais = null,
    bool BreakEvenAposParcial = false,
    decimal? TakeProfitPercentualPreco = null,
    decimal? StopLossPercentualPreco = null,
    decimal? TakeProfitPercentualConta = null,
    decimal? StopLossPercentualConta = null,
    decimal? TakeProfitValorBruto = null,
    decimal? StopLossValorBruto = null,
    decimal? TrailingStopPercentualPreco = null
);
