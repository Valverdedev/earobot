using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy;

public interface IEstrategiaEntrada
{
    string Nome { get; }

    /// <summary>
    /// Avalia se há sinal de entrada com base nos candles, indicadores calculados e tick.
    /// Retorna ResultadoDecisao (Comprar/Vender/Aguardar).
    /// </summary>
    ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config);

    /// <summary>
    /// Retorna o número mínimo de candles necessários para avaliação nativa da estratégia.
    /// Padrão é 0 para evitar quebra de compatibilidade em estratégias existentes.
    /// </summary>
    int ObterLookbackNecessario(EstrategiaConfig config) => 0;
}
