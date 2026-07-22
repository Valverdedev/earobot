using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class AvaliadorCondicoesTests
{
    private static ContextoAvaliacao CriarContexto(CandleMt5[] candles)
    {
        var config = new EstrategiaConfig("1", "Teste", 1, true, true, true, null, null, null, null, null, null);
        var tick = new TickEvent("T1", "WIN", 95, 95, new DateTime(2023, 1, 1));
        return new ContextoAvaliacao(candles, tick, config);
    }

    [Fact]
    public void Avaliar_Operador_Entre_DevePassarSeDentro()
    {
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 100, 150, 90, 120, 1000)]);
        var cond = new CondicaoDef { Op = "entre", A = "candle[0].fechamento", Min = "100", Max = "130" };
        
        var (passou, _) = AvaliadorCondicoes.AvaliarCondicao(cond, ctx);
        passou.Should().BeTrue();
    }

    [Fact]
    public void Avaliar_Operador_Entre_DeveFalharSeFora()
    {
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 100, 150, 90, 140, 1000)]);
        var cond = new CondicaoDef { Op = "entre", A = "candle[0].fechamento", Min = "100", Max = "130" };
        
        var (passou, _) = AvaliadorCondicoes.AvaliarCondicao(cond, ctx);
        passou.Should().BeFalse();
    }

    [Fact]
    public void Avaliar_Operador_Alinhados_DevePassarEfalhar()
    {
        // Médias hipotéticas na ordem: 10, 20, 30
        // Para mockar valores fixos sem rodar indicadores reais, usamos literais.
        var ctx = CriarContexto([]);
        
        var condCrescente = new CondicaoDef { Op = "alinhados", Serie = ["10", "20", "30"], Ordem = "crescente" };
        var (passCrescente, _) = AvaliadorCondicoes.AvaliarCondicao(condCrescente, ctx);
        passCrescente.Should().BeTrue();

        var condDecrescente = new CondicaoDef { Op = "alinhados", Serie = ["30", "20", "10"], Ordem = "decrescente" };
        var (passDecrescente, _) = AvaliadorCondicoes.AvaliarCondicao(condDecrescente, ctx);
        passDecrescente.Should().BeTrue();

        var condFalha = new CondicaoDef { Op = "alinhados", Serie = ["30", "20", "40"], Ordem = "crescente" };
        var (passFalha, _) = AvaliadorCondicoes.AvaliarCondicao(condFalha, ctx);
        passFalha.Should().BeFalse();
    }

    [Fact]
    public void Avaliar_Operador_Inclinacao_DevePassarEfalhar()
    {
        // Simulando que o valor atual é MAIOR que o anterior (literal[0] vs literal[1]) - hack
        // Inclinacao lê: v0 (atual) e vN (anterior N casas). 
        // Não conseguimos mockar diretamente o indice de um literal no contexto real.
        // Vamos usar candle.fechamento
        var ctx = CriarContexto([
            new CandleMt5(new DateTime(), 100, 100, 100, 50, 100), // C2 (Antigo)
            new CandleMt5(new DateTime(), 100, 100, 100, 100, 100), // C1
            new CandleMt5(new DateTime(), 100, 100, 100, 150, 100)  // C0 (Atual)
        ]);

        var condAlta = new CondicaoDef { Op = "inclinacao", A = "candle.fechamento", Candles = 2, Direcao = "alta" };
        var (passAlta, _) = AvaliadorCondicoes.AvaliarCondicao(condAlta, ctx);
        passAlta.Should().BeTrue(); // 150 > 50

        var condBaixa = new CondicaoDef { Op = "inclinacao", A = "candle.fechamento", Candles = 2, Direcao = "baixa" };
        var (passBaixa, _) = AvaliadorCondicoes.AvaliarCondicao(condBaixa, ctx);
        passBaixa.Should().BeFalse(); // 150 < 50 é falso
    }

    [Fact]
    public void Avaliar_Operador_PertoDe_DeveRespeitarTolerancia()
    {
        var ctx = CriarContexto([]);
        
        var cond1 = new CondicaoDef { Op = "pertoDe", A = "100", B = "102", ToleranciaPreco = 2 };
        AvaliadorCondicoes.AvaliarCondicao(cond1, ctx).Passou.Should().BeTrue();

        var cond2 = new CondicaoDef { Op = "pertoDe", A = "100", B = "103", ToleranciaPreco = 2 };
        AvaliadorCondicoes.AvaliarCondicao(cond2, ctx).Passou.Should().BeFalse();
    }

    [Fact]
    public void Avaliar_Operador_CruzouAcima_DevePassarEFalharCorretamente()
    {
        // c1 (antigo): fechamento = 100, abertura = 120 (A <= B)
        // c0 (atual):  fechamento = 130, abertura = 110 (A > B)
        var ctxCruzou = CriarContexto([
            new CandleMt5(new DateTime(), 120, 150, 90, 100, 1000), // c1
            new CandleMt5(new DateTime(), 110, 150, 90, 130, 1000)  // c0
        ]);
        var condCruzou = new CondicaoDef { Op = "cruzouAcima", A = "candle.fechamento", B = "candle.abertura" };
        AvaliadorCondicoes.AvaliarCondicao(condCruzou, ctxCruzou).Passou.Should().BeTrue();

        // c1 (antigo): fechamento = 130, abertura = 120 (A > B)
        // c0 (atual):  fechamento = 140, abertura = 110 (A > B) -> já estava acima!
        var ctxJaAcima = CriarContexto([
            new CandleMt5(new DateTime(), 120, 150, 90, 130, 1000), // c1
            new CandleMt5(new DateTime(), 110, 150, 90, 140, 1000)  // c0
        ]);
        AvaliadorCondicoes.AvaliarCondicao(condCruzou, ctxJaAcima).Passou.Should().BeFalse();
    }

    [Fact]
    public void Avaliar_Operador_CruzouAbaixo_DevePassarEFalharCorretamente()
    {
        // c1 (antigo): fechamento = 120, abertura = 100 (A >= B)
        // c0 (atual):  fechamento = 110, abertura = 130 (A < B)
        var ctxCruzou = CriarContexto([
            new CandleMt5(new DateTime(), 100, 150, 90, 120, 1000), // c1
            new CandleMt5(new DateTime(), 130, 150, 90, 110, 1000)  // c0
        ]);
        var condCruzou = new CondicaoDef { Op = "cruzouAbaixo", A = "candle.fechamento", B = "candle.abertura" };
        AvaliadorCondicoes.AvaliarCondicao(condCruzou, ctxCruzou).Passou.Should().BeTrue();

        // c1 (antigo): fechamento = 110, abertura = 130 (A < B)
        // c0 (atual):  fechamento = 100, abertura = 140 (A < B) -> já estava abaixo!
        var ctxJaAbaixo = CriarContexto([
            new CandleMt5(new DateTime(), 130, 150, 90, 110, 1000), // c1
            new CandleMt5(new DateTime(), 140, 150, 90, 100, 1000)  // c0
        ]);
        AvaliadorCondicoes.AvaliarCondicao(condCruzou, ctxJaAbaixo).Passou.Should().BeFalse();
    }

    [Fact]
    public void ResolverOperando_FibonacciRetracao_DeveCalcularAltaEBaixa()
    {
        var ctx = CriarContexto([
            new CandleMt5(new DateTime(2023, 1, 1, 10, 0, 0), 100, 110, 90, 105, 1000),
            new CandleMt5(new DateTime(2023, 1, 1, 10, 1, 0), 105, 120, 95, 115, 1000),
            new CandleMt5(new DateTime(2023, 1, 1, 10, 2, 0), 115, 130, 100, 125, 1000)
        ]);

        ParserOperando.TryParse("fibRet(0, 2, 0.618, \"alta\")", out var fibAlta, out _).Should().BeTrue();
        ParserOperando.TryParse("fibRet(0, 2, 0.618, \"baixa\")", out var fibBaixa, out _).Should().BeTrue();

        ctx.ResolverOperando(fibAlta!).Should().BeApproximately(105.28, 0.001);
        ctx.ResolverOperando(fibBaixa!).Should().BeApproximately(114.72, 0.001);
    }
}
