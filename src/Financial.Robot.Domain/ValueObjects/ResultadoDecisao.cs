namespace Financial.Robot.Domain.ValueObjects;

/// <summary>Resultado de uma decisão do Strategy Engine para um símbolo.</summary>
public sealed record ResultadoDecisao(
    bool Executar,
    LadoOrdem? Lado,
    string Motivo,
    double? StopSugerido = null
)
{
    /// <summary>Decisão negativa com motivo.</summary>
    public static ResultadoDecisao Aguardar(string motivo) => new(false, null, motivo);

    /// <summary>Decisão positiva para compra. StopSugerido, se informado, é um nível estrutural (ex: abaixo da barra de sinal) que tem prioridade sobre o SL genérico do SaidaConfig.</summary>
    public static ResultadoDecisao Comprar(string motivo, double? stopSugerido = null) => new(true, LadoOrdem.Compra, motivo, stopSugerido);

    /// <summary>Decisão positiva para venda. StopSugerido, se informado, é um nível estrutural (ex: acima da barra de sinal) que tem prioridade sobre o SL genérico do SaidaConfig.</summary>
    public static ResultadoDecisao Vender(string motivo, double? stopSugerido = null) => new(true, LadoOrdem.Venda, motivo, stopSugerido);
}
