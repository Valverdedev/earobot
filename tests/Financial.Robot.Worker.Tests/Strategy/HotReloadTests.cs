using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using Financial.Robot.Worker.Indicators;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class HotReloadTests : IDisposable
{
    private readonly string _testDir;
    private readonly string _testFile;

    public HotReloadTests()
    {
        _testDir = Path.Combine(Path.GetTempPath(), "EstrategiaTestes_" + Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDir);
        _testFile = Path.Combine(_testDir, "test.estrategia.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    [Fact]
    public void FileWatcher_DeveAtualizarCacheSeValido_OuManterAnteriorSeInvalido()
    {
        // 1. Criar arquivo inicial válido
        var conteudoValido = @"{
            ""$schemaVersion"": ""1.0"",
            ""nome"": ""Valido"",
            ""compra"": { ""setup"": { ""todas"": [ { ""op"": "">="", ""a"": ""candle[0].fechamento"", ""b"": 100 } ] } }
        }";
        File.WriteAllText(_testFile, conteudoValido);

        var config = new EstrategiaConfig("1", "Test", 1, true, true, true, null, null, null, null, null, 
            new Dictionary<string, object> { { "arquivoDefinicao", _testFile } });
        
        var estrategia = new EstrategiaInterpretada();
        var tick = new TickEvent("t1", "WIN", 100, 100, DateTime.UtcNow);
        var candles = new List<CandleMt5>();
        for (int i = 0; i < 30; i++) candles.Add(new CandleMt5(tick.Timestamp.AddMinutes(i), 100, 100, 100, 100, 0));

        // Act 1: Avaliar (carrega cache e registra watcher)
        var res1 = estrategia.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), tick, config);
        res1.Executar.Should().BeTrue();
        res1.Lado.Should().Be(LadoOrdem.Compra);

        // 2. Modificar para arquivo inválido
        var conteudoInvalido = @"{ ""compra"": { ""setup"": { ""todas"": [ { ""op"": ""bizarro"" } ] } } }";
        File.WriteAllText(_testFile, conteudoInvalido);
        
        // Esperar debounce do FileWatcher (500ms + folga)
        Thread.Sleep(800);

        // Act 2: Avaliar (deve manter o válido anterior)
        var res2 = estrategia.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), tick, config);
        res2.Executar.Should().BeTrue();
        res2.Lado.Should().Be(LadoOrdem.Compra);
        
        // 3. Modificar para um novo válido
        var conteudoValido2 = @"{
            ""$schemaVersion"": ""1.0"",
            ""nome"": ""Valido2"",
            ""venda"": { ""setup"": { ""todas"": [ { ""op"": ""<="", ""a"": ""candle[0].fechamento"", ""b"": 200 } ] } }
        }";
        File.WriteAllText(_testFile, conteudoValido2);

        // Esperar debounce
        Thread.Sleep(800);

        // Act 3: Avaliar (deve assumir a nova regra de venda)
        var res3 = estrategia.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), tick, config);
        res3.Executar.Should().BeTrue();
        res3.Lado.Should().Be(LadoOrdem.Venda);
    }
}
