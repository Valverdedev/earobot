using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Config;
using Financial.Robot.Worker.Execution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Financial.Robot.Worker.Tests.Execution;

public class GerenciadorSaidasParametricasTests
{
    private readonly IConnectionManager _connectionManager;
    private readonly ConfigWatcherService _configWatcher;
    private readonly IServicoExecucao _execucao;
    private readonly IGatewayMt5 _gateway;

    public GerenciadorSaidasParametricasTests()
    {
        _connectionManager = Substitute.For<IConnectionManager>();
        
        var loggerConfigValidator = NullLogger<ConfigValidator>.Instance;
        var configValidator = new ConfigValidator(loggerConfigValidator, _connectionManager);
        
        var loggerConfigWatcher = NullLogger<ConfigWatcherService>.Instance;
        var configuration = Substitute.For<IConfiguration>();
        _configWatcher = new ConfigWatcherService(loggerConfigWatcher, configuration, configValidator, _connectionManager);
        
        _execucao = Substitute.For<IServicoExecucao>();
        _gateway = Substitute.For<IGatewayMt5>();

        _connectionManager.GetClient(Arg.Any<string>()).Returns(_gateway);
    }

    private void InjectConfig(SymbolConfig config)
    {
        var activeConfigsField = typeof(ConfigWatcherService).GetField("_activeConfigs", BindingFlags.NonPublic | BindingFlags.Instance);
        var dict = (ConcurrentDictionary<string, SymbolConfig>)activeConfigsField!.GetValue(_configWatcher)!;
        dict[config.Symbol] = config;
    }

    private SymbolConfig CriarConfiguracao(SaidaConfig saida)
    {
        var estrat = new EstrategiaConfig("1", "E1", 0, true, true, true, null, saida, null, null, null, null);
        return new SymbolConfig("1", "WIN", "WIN", "T1", DateTime.UtcNow, "", true, true, true, 
            null!, saida, null!, null, null, "", "", null, new List<EstrategiaConfig> { estrat });
    }

    private SaidaConfig CriarSaida(decimal? tpBruto = null, decimal? tpPctConta = null)
    {
        return new SaidaConfig(
            TakeProfitValorBruto: tpBruto,
            TakeProfitPercentualConta: tpPctConta
        );
    }

    [Fact]
    public async Task DeveFecharPosicao_QuandoAtingirLimiteValorBruto()
    {
        // Arrange
        var saida = CriarSaida(tpBruto: 150.0m);
        var config = CriarConfiguracao(saida);

        InjectConfig(config);

        _gateway.ObterDetalhesPosicoesAbertasAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(new List<DetalhesPosicaoMt5>
            {
                new DetalhesPosicaoMt5(12345, "WIN", 0, true, 100000, 100100, 0, 0, 1.0, 160.0) // Lucro 160 >= 150
            }));

        _gateway.ObterInfoContaAsync(Arg.Any<CancellationToken>())
            .Returns(new InfoContaMt5(12345, "Server", "User", 10000.0, 10000.0, 10000.0, "BRL"));

        var sut = new GerenciadorPosicoesAbertasService(
            _configWatcher,
            _connectionManager,
            _execucao,
            new Financial.Robot.Worker.Strategy.CatalogoEstrategias(),
            new Financial.Robot.Worker.Indicators.CatalogoIndicadores(),
            new Financial.Robot.Worker.Indicators.CatalogoIndicadoresMultiFonte(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance),
            NullLogger<GerenciadorPosicoesAbertasService>.Instance);

        // Act
        var method = typeof(GerenciadorPosicoesAbertasService).GetMethod("ProcessarTodasAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;

        // Assert
        await _execucao.Received(1).FecharPosicaoAsync("T1", 12345, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeveIgnorarSaidaPercentualConta_SeSaldoIndisponivel()
    {
        // Arrange
        var saida = CriarSaida(tpPctConta: 5.0m);
        var config = CriarConfiguracao(saida);

        InjectConfig(config);

        _gateway.ObterDetalhesPosicoesAbertasAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(new List<DetalhesPosicaoMt5>
            {
                new DetalhesPosicaoMt5(12345, "WIN", 0, true, 100000, 100100, 0, 0, 1.0, 1000.0)
            }));

        // Simula falha ao obter saldo
        _gateway.ObterInfoContaAsync(Arg.Any<CancellationToken>()).Returns(Task.FromException<InfoContaMt5>(new System.Exception("Sem saldo")));

        var sut = new GerenciadorPosicoesAbertasService(
            _configWatcher,
            _connectionManager,
            _execucao,
            new Financial.Robot.Worker.Strategy.CatalogoEstrategias(),
            new Financial.Robot.Worker.Indicators.CatalogoIndicadores(),
            new Financial.Robot.Worker.Indicators.CatalogoIndicadoresMultiFonte(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance),
            NullLogger<GerenciadorPosicoesAbertasService>.Instance);

        // Act
        var method = typeof(GerenciadorPosicoesAbertasService).GetMethod("ProcessarTodasAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;

        // Assert
        await _execucao.DidNotReceive().FecharPosicaoAsync(Arg.Any<string>(), Arg.Any<ulong>(), Arg.Any<CancellationToken>());
    }
}
