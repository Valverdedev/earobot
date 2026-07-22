using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy;

public interface IEstrategiaSaida
{
    /// <summary>
    /// Avalia se a posição aberta deve ser encerrada por motivos técnicos.
    /// Retorna true se a posição deve ser fechada imediatamente.
    /// </summary>
    (bool Fechar, string Motivo) AvaliarSaida(
        bool isCompra,
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        DetalhesPosicaoMt5 posicao,
        TickEvent tick,
        EstrategiaConfig config);
}
