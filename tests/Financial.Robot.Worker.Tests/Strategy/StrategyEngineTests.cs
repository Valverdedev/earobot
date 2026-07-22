using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class StrategyEngineTests
{
    [Fact]
    public void CalcularMaxPeriodoRequerido_ComLookbackEstrategiaMaiorQueIndicadores_RetornaLookbackEstrategia()
    {
        var config = new EstrategiaConfig("id", "ScalperWinPullbackCurto", 999, true, true, true, null, null, null, null, null, null);
        var estrategia = new ScalperWinPullbackCurto();

        // Indicadores está vazio, então default é 50
        // Estratégia exige 105
        var resultado = StrategyEngine.CalcularMaxPeriodoRequerido(config, estrategia);

        resultado.Should().Be(105);
    }

    [Fact]
    public void CalcularMaxPeriodoRequerido_ComLookbackIndicadoresMaior_RetornaLookbackIndicadores()
    {
        var indicadores = new List<IndicadorConfig>
        {
            new IndicadorConfig("EMA", "M1", new Dictionary<string, object> { { "periodo", 200 } })
        };
        var config = new EstrategiaConfig("id", "CruzamentoEma", 999, true, true, true, null, null, null, null, indicadores, null);
        
        IEstrategiaEntrada? estrategia = null;

        var resultado = StrategyEngine.CalcularMaxPeriodoRequerido(config, estrategia);

        resultado.Should().Be(210); // 200 + 10
    }
}
