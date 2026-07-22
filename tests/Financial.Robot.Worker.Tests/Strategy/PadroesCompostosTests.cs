using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class PadroesCompostosTests
{
    private ContextoAvaliacao CriarContexto(List<CandleMt5> candles, Dictionary<string, object> configParametros)
    {
        var tick = new TickEvent("T1", "WIN", 100, 101, DateTime.UtcNow);
        var config = new EstrategiaConfig("1", "Test", 1, true, true, true, null, null, null, null, null, configParametros);
        return new ContextoAvaliacao(candles, tick, config);
    }

    [Fact]
    public void Avaliar_ImpulsoPullback_EncontraPadraoRegistraRef()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.Date.AddHours(10);
        var candles = new List<CandleMt5>
        {
            // Impulso de alta (3 candles)
            new CandleMt5(baseTime, 100, 110, 100, 109, 10),
            new CandleMt5(baseTime.AddMinutes(1), 109, 120, 108, 119, 10),
            new CandleMt5(baseTime.AddMinutes(2), 119, 130, 118, 129, 10),
            
            // Pullback (2 candles) sem violar o topo
            new CandleMt5(baseTime.AddMinutes(3), 129, 131, 125, 126, 10),
            new CandleMt5(baseTime.AddMinutes(4), 126, 128, 123, 124, 10) // Barra sinal = candle[0]
        };

        var ctx = CriarContexto(candles, new Dictionary<string, object>());
        var def = new CondicaoDef
        {
            Padrao = "impulsoPullback",
            Lado = "alta",
            IdRef = "sinalBar",
            MinCandlesImpulso = 2,
            MinCandlesPullback = 1,
            MaxPullbackPercentual = 0.5,
            CorpoMinimoFracaoRange = 0.5,
            InicioCandleSinal = 0 // testamos do índice 0 (vela 4)
        };

        // Act
        var result = PadroesCompostos.Avaliar(def, ctx, out var rastro);

        // Assert
        result.Should().BeTrue();
        rastro.Should().Contain("impulsoPullback OK");
        var cRef = ctx.ObterRef("sinalBar");
        cRef.Should().NotBeNull();
        cRef!.Fechamento.Should().Be(124);
    }
    
    [Fact]
    public void Avaliar_RompimentoReteste_ComTolerancia_AchaRompimento()
    {
        // Arrange
        var baseTime = DateTime.UtcNow.Date.AddHours(10);
        var candles = new List<CandleMt5>
        {
            new CandleMt5(baseTime, 95, 101, 94, 96, 0),
            // Candle 1 rompe acima do nivel 100 com tolerancia de 1
            new CandleMt5(baseTime.AddMinutes(1), 96, 105, 95, 103, 0),
            // Candle 0 retesta sem fechar abaixo de 100
            new CandleMt5(baseTime.AddMinutes(2), 103, 104, 101, 102, 0)
        };

        var jsonStr = "[{\"preco\": 100.0, \"tipo\": \"resistencia\"}]";
        var doc = JsonDocument.Parse(jsonStr);
        var configParams = new Dictionary<string, object>
        {
            { "niveis", doc.RootElement }
        };
        var ctx = CriarContexto(candles, configParams);
        var def = new CondicaoDef
        {
            Padrao = "rompimentoReteste",
            Lado = "alta",
            TipoNivel = "resistencia",
            ToleranciaPreco = 1,
            FimCandleSinal = 2,
            IdRef = "romp"
        };

        // Act
        var result = PadroesCompostos.Avaliar(def, ctx, out var rastro);

        // Assert
        result.Should().BeTrue();
        ctx.ObterRef("romp")!.Fechamento.Should().Be(103);
    }
}
