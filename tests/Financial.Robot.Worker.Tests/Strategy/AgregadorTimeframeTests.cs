using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using FluentAssertions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class AgregadorTimeframeTests
{
    [Fact]
    public void AgruparM1ParaTimeframeMaior_M5_AgrupaCorretamente()
    {
        // Arrange
        var baseTime = new DateTime(2026, 7, 17, 10, 2, 0); // 10:02
        var candles = new List<CandleMt5>
        {
            new CandleMt5(baseTime, 100, 105, 95, 102, 10),
            new CandleMt5(baseTime.AddMinutes(1), 102, 108, 101, 106, 15),
            new CandleMt5(baseTime.AddMinutes(2), 106, 107, 104, 105, 5), // 10:04
            new CandleMt5(baseTime.AddMinutes(3), 105, 110, 105, 109, 20), // 10:05 (novo chunk)
            new CandleMt5(baseTime.AddMinutes(4), 109, 112, 108, 111, 30)  // 10:06
        };

        // Act
        var result = AgregadorTimeframe.AgruparM1ParaTimeframeMaior(candles, "M5");

        // Assert
        result.Should().HaveCount(2);

        // Primeiro candle M5 (10:00 - engloba 10:02, 10:03, 10:04)
        result[0].Date.Should().Be(new DateTime(2026, 7, 17, 10, 0, 0));
        result[0].Open.Should().Be(100m);
        result[0].High.Should().Be(108m);
        result[0].Low.Should().Be(95m);
        result[0].Close.Should().Be(105m);
        result[0].Volume.Should().Be(30m);

        // Segundo candle M5 (10:05 - engloba 10:05 e 10:06)
        result[1].Date.Should().Be(new DateTime(2026, 7, 17, 10, 5, 0));
        result[1].Open.Should().Be(105m);
        result[1].High.Should().Be(112m);
        result[1].Low.Should().Be(105m);
        result[1].Close.Should().Be(111m);
        result[1].Volume.Should().Be(50m);
    }
}
