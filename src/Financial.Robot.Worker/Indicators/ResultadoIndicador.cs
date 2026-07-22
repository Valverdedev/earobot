namespace Financial.Robot.Worker.Indicators;

/// <summary>
/// Resultado genérico de um indicador técnico.
/// Campos opcionais para cobrir RSI, EMA, ATR, MACD com um único tipo.
/// </summary>
public sealed record ResultadoIndicador(
    DateTime Tempo,
    double? Valor,
    double? Sinal = null,
    double? Histograma = null
)
{
    /// <summary>Retorna verdadeiro se o indicador produziu um valor válido (sem NaN).</summary>
    public bool Valido => Valor.HasValue && !double.IsNaN(Valor.Value);
}
