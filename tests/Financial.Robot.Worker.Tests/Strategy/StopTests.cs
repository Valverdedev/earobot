using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class StopTests
{
    private static ContextoAvaliacao CriarContexto(CandleMt5[] candles)
    {
        var config = new EstrategiaConfig("1", "Teste", 1, true, true, true, null, null, null, null, null, null);
        var tick = new TickEvent("T1", "WIN", 95, 95, new DateTime(2023, 1, 1));
        return new ContextoAvaliacao(candles, tick, config);
    }

    [Fact]
    public void Calcular_ExtremoCandle_DeveRetornarComBuffer()
    {
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 100, 150, 90, 110, 1000)]);
        
        // Compra -> Stop abaixo da mínima
        var stopCompra = new StopDef { Tipo = "extremoCandle", Candle = 0, Lado = "minimo", BufferPreco = 5 };
        var calcCompra = CalculadoraStop.Calcular(stopCompra, ctx, true);
        calcCompra.Should().Be(85); // 90 - 5

        // Venda -> Stop acima da máxima
        var stopVenda = new StopDef { Tipo = "extremoCandle", Candle = 0, Lado = "maximo", BufferPreco = 5 };
        var calcVenda = CalculadoraStop.Calcular(stopVenda, ctx, false);
        calcVenda.Should().Be(155); // 150 + 5
    }

    [Fact]
    public void Calcular_ValorOperando_DeveRetornarComBuffer()
    {
        var ctx = CriarContexto([new CandleMt5(new DateTime(2023, 1, 1), 100, 150, 90, 110, 1000)]);
        
        // Operando explicitado
        var stopCompra = new StopDef { Tipo = "valorOperando", A = "candle[0].abertura", BufferPreco = 10 };
        var calcCompra = CalculadoraStop.Calcular(stopCompra, ctx, true);
        calcCompra.Should().Be(90); // 100 - 10

        var stopVenda = new StopDef { Tipo = "valorOperando", A = "candle[0].abertura", BufferPreco = 10 };
        var calcVenda = CalculadoraStop.Calcular(stopVenda, ctx, false);
        calcVenda.Should().Be(110); // 100 + 10
    }
}
