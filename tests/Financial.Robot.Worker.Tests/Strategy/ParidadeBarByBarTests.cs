using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Strategy.Estrategias;
using Financial.Robot.Worker.Strategy.Interpretada;
using System.Collections.Generic;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class ParidadeBarByBarTests
{
    [Fact]
    public void BarByBar_ParidadeDsl_SerieSintetica()
    {
        var compilada = new PriceActionBarByBar();
        var interpretada = new EstrategiaInterpretada();
        
        var parametrosComuns = new Dictionary<string, object>
        {
            { "corpoMinimoFracaoRange", 0.55 },
            { "candlesImpulsoMinimo", 2 },
            { "candlesImpulsoMax", 6 },
            { "candlesPullbackMinimo", 1 },
            { "candlesPullbackMax", 5 },
            { "pullbackMaximoPercentual", 0.7 },
            { "lookbackMedioRange", 20 },
            { "climaxMultiploRange", 2.5 },
            { "bufferStopPreco", 5.0 }
        };

        var configCompilada = new EstrategiaConfig(
            "id-compilada", "PriceActionBarByBar", 1, true, true, true, 
            null, null, null, null, new List<IndicadorConfig>(), parametrosComuns);

        var parametrosInterp = new Dictionary<string, object>(parametrosComuns)
        {
            { "arquivoDefinicao", "../Fixtures/barbybar-paridade.estrategia.json" }
        };

        var configInterpretada = new EstrategiaConfig(
            "id-interp", "EstrategiaInterpretada", 1, true, true, true, 
            null, null, null, null, new List<IndicadorConfig>(), parametrosInterp);

        var historico = HarnessParidade.GerarSerieRealista(2);

        HarnessParidade.AvaliarParidade(
            compilada, interpretada, historico, configCompilada, configInterpretada, injetarRsi14: false);
    }

    [Fact]
    public void BarByBar_ParidadeDsl_FlagsDesligadas()
    {
        var compilada = new PriceActionBarByBar();
        var interpretada = new EstrategiaInterpretada();
        
        var parametrosComuns = new Dictionary<string, object>
        {
            { "corpoMinimoFracaoRange", 0.55 },
            { "candlesImpulsoMinimo", 2 },
            { "candlesImpulsoMax", 6 },
            { "candlesPullbackMinimo", 1 },
            { "candlesPullbackMax", 5 },
            { "pullbackMaximoPercentual", 0.7 },
            { "lookbackMedioRange", 20 },
            { "climaxMultiploRange", 2.5 },
            { "bufferStopPreco", 5.0 }
        };

        var configCompilada = new EstrategiaConfig("id-c", "PriceActionBarByBar", 1, true, false, false, null, null, null, null, new List<IndicadorConfig>(), parametrosComuns);
        var configInterpretada = new EstrategiaConfig("id-i", "EstrategiaInterpretada", 1, true, false, false, null, null, null, null, new List<IndicadorConfig>(), 
            new Dictionary<string, object>(parametrosComuns) { { "arquivoDefinicao", "../Fixtures/barbybar-paridade.estrategia.json" } });

        var historico = HarnessParidade.GerarSerieRealista(2);
        HarnessParidade.AvaliarParidade(compilada, interpretada, historico, configCompilada, configInterpretada, injetarRsi14: false);
    }
}
