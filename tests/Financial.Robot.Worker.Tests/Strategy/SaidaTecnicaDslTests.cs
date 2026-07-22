using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Strategy.Interpretada;
using Financial.Robot.Worker.Indicators;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.IO;
using Financial.Robot.Domain.Interfaces;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public sealed class SaidaTecnicaDslTests : IDisposable
{
    private readonly string _mockVazioPath;
    private readonly string _mockSaidaPath;

    public SaidaTecnicaDslTests()
    {
        var basePath = Directory.GetCurrentDirectory();
        var configPath = Path.Combine(basePath, "config");
        if (!Directory.Exists(configPath)) Directory.CreateDirectory(configPath);
        
        _mockVazioPath = Path.Combine(configPath, "mock-vazio.json");
        _mockSaidaPath = Path.Combine(configPath, "mock-saida.json");
        
        System.IO.File.WriteAllText(_mockVazioPath, System.Text.Json.JsonSerializer.Serialize(new DefinicaoEstrategia { SchemaVersion = "1.0", Compra = new BlocoLado { Setup = new CondicaoDef { Op = "==", A = "1", B = "1" } } }));
        
        System.IO.File.WriteAllText(_mockSaidaPath, System.Text.Json.JsonSerializer.Serialize(new DefinicaoEstrategia
        {
            SchemaVersion = "1.0",
            Compra = new BlocoLado { Setup = new CondicaoDef { Op = "==", A = "1", B = "1" } },
            Saida = new SaidaDefinicao { Compra = new CondicaoDef { Op = ">", A = "posicao.lucroPercentualPreco", B = "1.0" }, Venda = new CondicaoDef { Op = "<", A = "posicao.lucroBruto", B = "-10.0" } }
        }));
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(_mockVazioPath)) System.IO.File.Delete(_mockVazioPath);
        if (System.IO.File.Exists(_mockSaidaPath)) System.IO.File.Delete(_mockSaidaPath);
    }

    [Fact]
    public void SaidaTecnica_SemBlocoSaida_RetornaFalseSempre()
    {
        var estrategia = new EstrategiaInterpretada();
        var config = new EstrategiaConfig("teste", "teste", 1, true, true, true, null, null, null, null, null, new Dictionary<string, object> { { "arquivoDefinicao", "mock-vazio.json" } });
        
        var pos = new DetalhesPosicaoMt5(1, "WIN", 123, true, 100, 110, 10, 90, 10, 1);
        var tick = new TickEvent("T", "S", 100, 101, DateTime.UtcNow);
        var (fechar, _) = estrategia.AvaliarSaida(true, new List<CandleMt5>(), new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), pos, tick, config);
        
        fechar.Should().BeFalse();
    }

    [Fact]
    public void SaidaTecnica_PosicaoComprada_IgnoraVenda_E_AvaliaCompra()
    {
        var estrategia = new EstrategiaInterpretada();
        var config = new EstrategiaConfig("teste", "teste", 1, true, true, true, null, null, null, null, null, new Dictionary<string, object> { { "arquivoDefinicao", "mock-saida.json" } });
        
        // Lucro de +5% na compra (PrecoAbertura = 100, Atual = 105) -> maior que 1.0 -> dispara
        var pos = new DetalhesPosicaoMt5(1, "WIN", 123, true, 100.0, 105.0, 90.0, 200.0, 50.0, 1.0);
        var tick = new TickEvent("T", "S", 105, 106, DateTime.UtcNow);
        var (fechar, motivo) = estrategia.AvaliarSaida(true, new List<CandleMt5>(), new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), pos, tick, config);
        
        fechar.Should().BeTrue();
        motivo.Should().Contain("saida/compra");
    }

    [Fact]
    public void SaidaTecnica_PosicaoVendida_IgnoraCompra_E_AvaliaVenda()
    {
        var estrategia = new EstrategiaInterpretada();
        var config = new EstrategiaConfig("teste", "teste", 1, true, true, true, null, null, null, null, null, new Dictionary<string, object> { { "arquivoDefinicao", "mock-saida.json" } });
        
        var pos = new DetalhesPosicaoMt5(1, "WIN", 123, false, 100.0, 95.0, 110.0, 50.0, 50.0, -20.0);
        var tick = new TickEvent("T", "S", 95, 96, DateTime.UtcNow);
        var (fechar, motivo) = estrategia.AvaliarSaida(false, new List<CandleMt5>(), new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), pos, tick, config);
        
        // old: 10 > 9.5, new: 9 < 9.5 -> Cruzou Abaixo!
        fechar.Should().BeTrue();
        motivo.Should().Contain("saida/venda");
    }
    
    [Fact]
    public void SaidaTecnica_LucroPercentual_SinalCorreto_Para_Ambos_Os_Lados()
    {
        // Compra (abriu 100, atual 102) -> +2% lucro
        var posCompra = new DetalhesPosicaoMt5(1, "WIN", 123, true, 100.0, 102.0, 0, 0, 20.0, 1.0);
        var tickC = new TickEvent("T", "S", 102, 102, DateTime.UtcNow);
        var configC = new EstrategiaConfig("T", "T", 1, true, true, true, null, null, null, null, null, new Dictionary<string, object>());
        var ctxC = new ContextoAvaliacao(new List<CandleMt5>(), tickC, configC, posCompra);
        
        ParserOperando.TryParse("posicao.lucroPercentualPreco", out var opC, out _);
        var valC = ctxC.ResolverOperando(opC);
        valC.Should().BeApproximately(2.0, 0.001);

        // Venda (abriu 100, atual 98) -> +2% lucro
        var posVenda = new DetalhesPosicaoMt5(2, "WIN", 123, false, 100.0, 98.0, 0, 0, 20.0, 1.0);
        var tickV = new TickEvent("T", "S", 98, 98, DateTime.UtcNow);
        var ctxV = new ContextoAvaliacao(new List<CandleMt5>(), tickV, configC, posVenda);
        
        ParserOperando.TryParse("posicao.lucroPercentualPreco", out var opV, out _);
        var valV = ctxV.ResolverOperando(opV);
        valV.Should().BeApproximately(2.0, 0.001);
    }
}
