using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Strategy.Estrategias;
using Financial.Robot.Worker.Strategy.Interpretada;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class ParidadeReversaoRangeTests
{
    [Fact]
    public void ReversaoRange_ParidadeDsl_SerieSintetica()
    {
        var compilada = new ReversaoRange();
        var interpretada = new EstrategiaInterpretada();
        
        var jsonStr = "[{\"preco\": 100000.0, \"tipo\": \"suporte\"}, {\"preco\": 100500.0, \"tipo\": \"resistencia\"}]";
        var doc = JsonDocument.Parse(jsonStr);

        var parametrosComuns = new Dictionary<string, object>
        {
            { "rsiSobrevendaMaximo", 30.0 },
            { "rsiSobrecompraMinimo", 70.0 },
            { "distanciaMaximaDoNivelPontos", 100.0 },
            { "niveis", doc.RootElement }
        };

        var configCompilada = new EstrategiaConfig(
            "id-compilada", "ReversaoRange", 1, true, true, true, 
            null, null, null, null, new List<IndicadorConfig>(), parametrosComuns);

        var parametrosInterp = new Dictionary<string, object>(parametrosComuns)
        {
            { "arquivoDefinicao", "../Fixtures/reversao-range-paridade.estrategia.json" }
        };

        var configInterpretada = new EstrategiaConfig(
            "id-interp", "EstrategiaInterpretada", 1, true, true, true, 
            null, null, null, null, new List<IndicadorConfig>(), parametrosInterp);

        var historico = HarnessParidade.GerarSerieRealista(2);

        HarnessParidade.AvaliarParidade(
            compilada, interpretada, historico, configCompilada, configInterpretada, injetarRsi14: true);
    }

    [Fact]
    public void ReversaoRange_ParidadeDsl_SemNiveis()
    {
        var compilada = new ReversaoRange();
        var interpretada = new EstrategiaInterpretada();
        
        var jsonStr = "[]"; // Sem níveis configurados
        var doc = JsonDocument.Parse(jsonStr);

        var parametrosComuns = new Dictionary<string, object>
        {
            { "rsiSobrevendaMaximo", 30.0 },
            { "rsiSobrecompraMinimo", 70.0 },
            { "distanciaMaximaDoNivelPontos", 100.0 },
            { "niveis", doc.RootElement }
        };

        var configCompilada = new EstrategiaConfig("id-c", "ReversaoRange", 1, true, true, true, null, null, null, null, new List<IndicadorConfig>(), parametrosComuns);
        var configInterpretada = new EstrategiaConfig("id-i", "EstrategiaInterpretada", 1, true, true, true, null, null, null, null, new List<IndicadorConfig>(), 
            new Dictionary<string, object>(parametrosComuns) { { "arquivoDefinicao", "../Fixtures/reversao-range-paridade.estrategia.json" } });

        var historico = HarnessParidade.GerarSerieRealista(2);
        HarnessParidade.AvaliarParidade(compilada, interpretada, historico, configCompilada, configInterpretada, injetarRsi14: true);
    }

    [Fact]
    public void ReversaoRange_ParidadeDsl_FlagsDesligadas()
    {
        var compilada = new ReversaoRange();
        var interpretada = new EstrategiaInterpretada();
        
        var jsonStr = "[{\"preco\": 100000.0, \"tipo\": \"suporte\"}, {\"preco\": 100500.0, \"tipo\": \"resistencia\"}]";
        var doc = JsonDocument.Parse(jsonStr);

        var parametrosComuns = new Dictionary<string, object>
        {
            { "rsiSobrevendaMaximo", 30.0 },
            { "rsiSobrecompraMinimo", 70.0 },
            { "distanciaMaximaDoNivelPontos", 100.0 },
            { "niveis", doc.RootElement }
        };

        var configCompilada = new EstrategiaConfig("id-c", "ReversaoRange", 1, true, false, false, null, null, null, null, new List<IndicadorConfig>(), parametrosComuns);
        var configInterpretada = new EstrategiaConfig("id-i", "EstrategiaInterpretada", 1, true, false, false, null, null, null, null, new List<IndicadorConfig>(), 
            new Dictionary<string, object>(parametrosComuns) { { "arquivoDefinicao", "../Fixtures/reversao-range-paridade.estrategia.json" } });

        var historico = HarnessParidade.GerarSerieRealista(2);
        HarnessParidade.AvaliarParidade(compilada, interpretada, historico, configCompilada, configInterpretada, injetarRsi14: true);
    }
}
