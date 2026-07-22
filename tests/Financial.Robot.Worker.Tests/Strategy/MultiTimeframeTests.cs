using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class MultiTimeframeTests
{
    [Fact]
    public void ContextoAvaliacao_IndicadorComTimeframeMaior_CalculaCorretamente()
    {
        // Arrange
        var baseTime = new DateTime(2026, 7, 17, 10, 0, 0);
        var candles = new List<CandleMt5>();
        for (int i = 0; i < 15; i++)
        {
            candles.Add(new CandleMt5(baseTime.AddMinutes(i), 100, 110, 95, 105, 10));
        }

        var tick = new TickEvent("T1", "WIN", 105, 106, baseTime.AddMinutes(15));
        var config = new EstrategiaConfig("1", "Test", 1, true, true, true, null, null, null, null, null, new Dictionary<string, object>());
        var ctx = new ContextoAvaliacao(candles, tick, config);

        // Act
        // SMA de período 2 no timeframe M5
        var op = new Operando { Fonte = TipoFonte.Sma, Periodo = 2, Timeframe = "M5", Indice = 0 };
        var smaM5 = ctx.ResolverOperando(op);
        
        var opSmaNormal = new Operando { Fonte = TipoFonte.Sma, Periodo = 2, Indice = 0 };
        var smaNormal = ctx.ResolverOperando(opSmaNormal);

        // Assert
        // Em M5, temos 3 candles consolidados (10:00, 10:05, 10:10). Todos têm fechamento 105. A SMA M5(2) será 105.
        smaM5.Should().Be(105);
        smaNormal.Should().Be(105);

        // E se testarmos candle[0] no M5?
        var opCandleM5 = new Operando { Fonte = TipoFonte.CandleVolume, Timeframe = "M5", Indice = 0 };
        var volM5 = ctx.ResolverOperando(opCandleM5);
        
        // Em M5, cada agrupamento tem 5 candles de M1, logo o volume será 10 * 5 = 50.
        volM5.Should().Be(50);
    }
}
