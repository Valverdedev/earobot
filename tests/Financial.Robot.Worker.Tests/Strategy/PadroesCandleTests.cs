using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class PadroesCandleTests
{
    private static ContextoAvaliacao CriarContexto(CandleMt5[] candles)
    {
        var config = new EstrategiaConfig("1", "Teste", 1, true, true, true, null, null, null, null, null, null);
        var tick = new TickEvent("T1", "WIN", 95, 95, new DateTime(2023, 1, 1));
        return new ContextoAvaliacao(candles, tick, config);
    }

    [Fact]
    public void Avaliar_TrendBarAlta_DevePassarEFalharCorretamente()
    {
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 100, 150, 90, 145, 1000)]);
        var cond = new CondicaoDef { Padrao = "trendBarAlta", Candle = 0, CorpoMinimoFracaoRange = 0.5 };

        PadroesCandle.Avaliar(cond, ctx, out var rastro).Should().BeTrue();

        var condFalha = new CondicaoDef { Padrao = "trendBarAlta", Candle = 0, CorpoMinimoFracaoRange = 0.9 };
        PadroesCandle.Avaliar(condFalha, ctx, out _).Should().BeFalse();
    }

    [Fact]
    public void Avaliar_Sequencia_DevePassarEFalharCorretamente()
    {
        var ctx = CriarContexto([
            new CandleMt5(new DateTime(), 100, 120, 90, 110, 100), // C2 Alta
            new CandleMt5(new DateTime(), 110, 130, 100, 120, 100), // C1 Alta
            new CandleMt5(new DateTime(), 120, 140, 110, 130, 100)  // C0 Alta
        ]);

        var condAlta = new CondicaoDef { Padrao = "sequencia", Candles = 3, Lado = "alta", Candle = 0 };
        PadroesCandle.Avaliar(condAlta, ctx, out _).Should().BeTrue();

        var condBaixa = new CondicaoDef { Padrao = "sequencia", Candles = 3, Lado = "baixa", Candle = 0 };
        PadroesCandle.Avaliar(condBaixa, ctx, out _).Should().BeFalse();
    }

    [Fact]
    public void Avaliar_RejeicaoVendedora_DevePassar()
    {
        // Pavio superior muito maior que o corpo, corpo negativo
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 120, 180, 110, 115, 1000)]);
        var cond = new CondicaoDef { Padrao = "rejeicaoVendedora", Candle = 0, MultiploPavio = 2.0 };

        PadroesCandle.Avaliar(cond, ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void Avaliar_TrendBarBaixa_DevePassarEFalharCorretamente()
    {
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 150, 160, 90, 100, 1000)]);
        var cond = new CondicaoDef { Padrao = "trendBarBaixa", Candle = 0, CorpoMinimoFracaoRange = 0.5 };
        
        PadroesCandle.Avaliar(cond, ctx, out _).Should().BeTrue();

        var condFalha = new CondicaoDef { Padrao = "trendBarBaixa", Candle = 0, CorpoMinimoFracaoRange = 0.8 };
        PadroesCandle.Avaliar(condFalha, ctx, out _).Should().BeFalse();
    }

    [Fact]
    public void Avaliar_RejeicaoCompradora_DevePassar()
    {
        // Pavio inferior muito maior que o corpo, corpo positivo
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 110, 120, 50, 115, 1000)]);
        var cond = new CondicaoDef { Padrao = "rejeicaoCompradora", Candle = 0, MultiploPavio = 2.0 };

        PadroesCandle.Avaliar(cond, ctx, out _).Should().BeTrue();
    }

    [Fact]
    public void Avaliar_FechamentoDirecional_DevePassar()
    {
        var ctxAlta = CriarContexto([new CandleMt5(new DateTime(), 100, 120, 90, 110, 1000)]);
        var condAlta = new CondicaoDef { Padrao = "fechamentoDirecional", Lado = "alta", Candle = 0 };
        PadroesCandle.Avaliar(condAlta, ctxAlta, out _).Should().BeTrue();

        var ctxBaixa = CriarContexto([new CandleMt5(new DateTime(), 110, 120, 90, 100, 1000)]);
        var condBaixa = new CondicaoDef { Padrao = "fechamentoDirecional", Lado = "baixa", Candle = 0 };
        PadroesCandle.Avaliar(condBaixa, ctxBaixa, out _).Should().BeTrue();
    }

    [Fact]
    public void Avaliar_InsideBar_DevePassarEFalhar()
    {
        var ctxPassa = CriarContexto([
            new CandleMt5(new DateTime(), 100, 150, 50, 100, 1000), // c1
            new CandleMt5(new DateTime(), 100, 120, 80, 100, 1000)  // c0
        ]);
        var cond = new CondicaoDef { Padrao = "insideBar", Candle = 0 };
        PadroesCandle.Avaliar(cond, ctxPassa, out _).Should().BeTrue();

        var ctxFalha = CriarContexto([
            new CandleMt5(new DateTime(), 100, 120, 80, 100, 1000), // c1
            new CandleMt5(new DateTime(), 100, 150, 50, 100, 1000)  // c0
        ]);
        PadroesCandle.Avaliar(cond, ctxFalha, out _).Should().BeFalse();
    }

    [Fact]
    public void Avaliar_OutsideBar_DevePassarEFalhar()
    {
        var ctxPassa = CriarContexto([
            new CandleMt5(new DateTime(), 100, 120, 80, 100, 1000), // c1
            new CandleMt5(new DateTime(), 100, 150, 50, 100, 1000)  // c0
        ]);
        var cond = new CondicaoDef { Padrao = "outsideBar", Candle = 0 };
        PadroesCandle.Avaliar(cond, ctxPassa, out _).Should().BeTrue();

        var ctxFalha = CriarContexto([
            new CandleMt5(new DateTime(), 100, 150, 50, 100, 1000), // c1
            new CandleMt5(new DateTime(), 100, 120, 80, 100, 1000)  // c0
        ]);
        PadroesCandle.Avaliar(cond, ctxFalha, out _).Should().BeFalse();
    }

    [Fact]
    public void Avaliar_EngolfoAlta_DevePassarEFalhar()
    {
        var ctxPassa = CriarContexto([
            new CandleMt5(new DateTime(), 120, 130, 90, 100, 1000), // c1 (baixa)
            new CandleMt5(new DateTime(), 90, 140, 80, 130, 1000)   // c0 (alta) engolfando o corpo
        ]);
        var cond = new CondicaoDef { Padrao = "engolfoAlta", Candle = 0 };
        PadroesCandle.Avaliar(cond, ctxPassa, out _).Should().BeTrue();

        var ctxFalha = CriarContexto([
            new CandleMt5(new DateTime(), 100, 130, 90, 120, 1000), // c1 (alta)
            new CandleMt5(new DateTime(), 90, 140, 80, 130, 1000)   // c0 (alta) 
        ]);
        PadroesCandle.Avaliar(cond, ctxFalha, out _).Should().BeFalse(); // O anterior não é baixa
    }

    [Fact]
    public void Avaliar_EngolfoBaixa_DevePassarEFalhar()
    {
        var ctxPassa = CriarContexto([
            new CandleMt5(new DateTime(), 100, 130, 90, 120, 1000), // c1 (alta)
            new CandleMt5(new DateTime(), 130, 140, 80, 90, 1000)   // c0 (baixa) engolfando o corpo
        ]);
        var cond = new CondicaoDef { Padrao = "engolfoBaixa", Candle = 0 };
        PadroesCandle.Avaliar(cond, ctxPassa, out _).Should().BeTrue();

        var ctxFalha = CriarContexto([
            new CandleMt5(new DateTime(), 120, 130, 90, 100, 1000), // c1 (baixa)
            new CandleMt5(new DateTime(), 130, 140, 80, 90, 1000)   // c0 (baixa) 
        ]);
        PadroesCandle.Avaliar(cond, ctxFalha, out _).Should().BeFalse();
    }
}
