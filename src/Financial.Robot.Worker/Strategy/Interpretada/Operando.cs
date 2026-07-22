namespace Financial.Robot.Worker.Strategy.Interpretada;

public enum TipoFonte
{
    // Candle
    CandleAbertura,
    CandleMaximo,
    CandleMinimo,
    CandleFechamento,
    CandleVolume,
    CandleRange,
    CandleCorpo,
    CandleCorpoAbs,
    CandlePavioSuperior,
    CandlePavioInferior,
    CandlePosFechamentoRange,

    // Tick
    TickBid,
    TickAsk,
    TickSpread,

    // Indicadores
    Ema,
    Sma,
    Smma,
    Rsi,
    Atr,
    Vwap,
    MediaPrecoMediano,

    // Niveis
    NivelSuportePreco,
    NivelResistenciaPreco,

    // Agregados
    Maxima,
    Minima,
    MaximaJanela,
    MinimaJanela,
    RangeMedio,
    FibonacciRetracao,

    // Literal
    Literal,

    // Refs
    Ref,

    // Posicao (Saida técnica)
    PosicaoPrecoEntrada,
    PosicaoLucroBruto,
    PosicaoLucroPercentualPreco
}

public sealed record Operando
{
    public TipoFonte Fonte { get; init; }
    
    public string? ParametroString { get; init; } 
    public int? Periodo { get; init; }
    public int? Periodo2 { get; init; } 

    public int? Indice { get; init; } 
    
    public char? Modificador { get; init; } 
    public double? ValorModificador { get; init; }
    
    public double? ValorLiteral { get; init; }
    public string? Timeframe { get; init; }
    public double? NivelFibonacci { get; init; }
    public string? DirecaoFibonacci { get; init; }
}
