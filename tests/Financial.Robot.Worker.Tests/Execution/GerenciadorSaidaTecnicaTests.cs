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
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Indicators;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Financial.Robot.Worker.Tests.Execution;

public class GerenciadorSaidaTecnicaTests
{
    private readonly IConnectionManager _connectionManager;
    private readonly ConfigWatcherService _configWatcher;
    private readonly IServicoExecucao _execucao;
    private readonly IGatewayMt5 _gateway;
    private readonly CatalogoEstrategias _catalogoEstrategias;

    public GerenciadorSaidaTecnicaTests()
    {
        _connectionManager = Substitute.For<IConnectionManager>();
        
        var loggerConfigValidator = NullLogger<ConfigValidator>.Instance;
        var configValidator = new ConfigValidator(loggerConfigValidator, _connectionManager);
        
        var loggerConfigWatcher = NullLogger<ConfigWatcherService>.Instance;
        var configuration = Substitute.For<IConfiguration>();
        _configWatcher = new ConfigWatcherService(loggerConfigWatcher, configuration, configValidator, _connectionManager);
        
        _execucao = Substitute.For<IServicoExecucao>();
        _gateway = Substitute.For<IGatewayMt5>();
        _catalogoEstrategias = new CatalogoEstrategias();

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
        var estrat = new EstrategiaConfig("fake", "Fake", 0, true, true, true, null, saida, null, null, null, null);
        return new SymbolConfig("1", "WIN", "WIN", "T1", DateTime.UtcNow, "", true, true, true, 
            null!, saida, null!, null, null, "", "", null, new List<EstrategiaConfig> { estrat });
    }

    [Fact]
    public async Task NaoVerificaTecnica_QuandoNenhumNovoCandleFoiFechado()
    {
        var saida = new SaidaConfig();
        var config = CriarConfiguracao(saida);
        InjectConfig(config);

        var estratSaidaObj = Substitute.For<IEstrategiaTeste>();
        estratSaidaObj.AvaliarSaida(Arg.Any<bool>(), Arg.Any<IReadOnlyList<CandleMt5>>(), Arg.Any<IReadOnlyList<(IndicadorConfig, ResultadoIndicador)>>(), Arg.Any<IReadOnlyList<(IndicadorConfig, ResultadoIndicador)>>(), Arg.Any<DetalhesPosicaoMt5>(), Arg.Any<Domain.Events.TickEvent>(), Arg.Any<EstrategiaConfig>())
            .Returns((true, "saida/compra"));
        
        estratSaidaObj.Nome.Returns("fake");
        _catalogoEstrategias.Registrar(estratSaidaObj);
        
        int callCount = 0;
        _gateway.ObterDetalhesPosicoesAbertasAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(x => 
            {
                callCount++;
                if (callCount == 1)
                {
                    return Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(new List<DetalhesPosicaoMt5>
                    {
                        new DetalhesPosicaoMt5(12345, "WIN", 0, true, 100000, 100100, 0, 0, 1.0, 10.0)
                    });
                }
                return Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(new List<DetalhesPosicaoMt5>());
            });

        var timeBase = new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc);
        _gateway.ObterCandlesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<CandleMt5> { new CandleMt5(timeBase, 1, 2, 1, 2, 100) });

        var sut = new GerenciadorPosicoesAbertasService(
            _configWatcher,
            _connectionManager,
            _execucao,
            _catalogoEstrategias,
            null!, // _catalogoIndicadores
            new Financial.Robot.Worker.Indicators.CatalogoIndicadoresMultiFonte(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance), // _catalogoMultiFonte
            NullLogger<GerenciadorPosicoesAbertasService>.Instance);

        var method = typeof(GerenciadorPosicoesAbertasService).GetMethod("ProcessarTodasAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Ciclo 1
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;
        
        // Ciclo 2 (mesmo candle)
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;

        // A avaliação (e a extração maior de candles/indicadores) só ocorreu na primeira vez.
        // A segunda chamada ignorou a etapa IEstrategiaSaida por ter a mesma data do candle 0.
        // Devido ao mock estratSaidaObj retornar true, esperamos que FecharPosicao tenha sido chamado na 1a.
        await _execucao.Received(1).FecharPosicaoAsync("T1", 12345, Arg.Any<CancellationToken>());
    }

    public interface IEstrategiaTeste : IEstrategiaEntrada, IEstrategiaSaida {}

    [Fact]
    public async Task VerificaTecnica_QuandoUmNovoCandleForFechado()
    {
        var saida = new SaidaConfig();
        var config = CriarConfiguracao(saida);
        InjectConfig(config);

        var estratSaidaObj = Substitute.For<IEstrategiaTeste>();
        estratSaidaObj.AvaliarSaida(Arg.Any<bool>(), Arg.Any<IReadOnlyList<CandleMt5>>(), Arg.Any<IReadOnlyList<(IndicadorConfig, ResultadoIndicador)>>(), Arg.Any<IReadOnlyList<(IndicadorConfig, ResultadoIndicador)>>(), Arg.Any<DetalhesPosicaoMt5>(), Arg.Any<Domain.Events.TickEvent>(), Arg.Any<EstrategiaConfig>())
            .Returns((false, "")); // Não fecha na 1a vez
        
        estratSaidaObj.Nome.Returns("fake");
        _catalogoEstrategias.Registrar(estratSaidaObj);
        
        _gateway.ObterDetalhesPosicoesAbertasAsync(Arg.Any<string>(), Arg.Any<long>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IReadOnlyList<DetalhesPosicaoMt5>>(new List<DetalhesPosicaoMt5>
            {
                new DetalhesPosicaoMt5(12345, "WIN", 0, true, 100000, 100100, 0, 0, 1.0, 10.0)
            }));

        var timeBase1 = new DateTime(2026, 7, 18, 10, 0, 0, DateTimeKind.Utc);
        var timeBase2 = new DateTime(2026, 7, 18, 10, 1, 0, DateTimeKind.Utc);
        
        _gateway.ObterCandlesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<CandleMt5> { new CandleMt5(timeBase1, 1, 2, 1, 2, 100) });

        var sut = new GerenciadorPosicoesAbertasService(
            _configWatcher,
            _connectionManager,
            _execucao,
            _catalogoEstrategias,
            null!, // _catalogoIndicadores
            new Financial.Robot.Worker.Indicators.CatalogoIndicadoresMultiFonte(Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance), // _catalogoMultiFonte
            NullLogger<GerenciadorPosicoesAbertasService>.Instance);

        var method = typeof(GerenciadorPosicoesAbertasService).GetMethod("ProcessarTodasAsync", BindingFlags.NonPublic | BindingFlags.Instance);
        
        // Ciclo 1 (retorna false)
        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;
        await _execucao.DidNotReceive().FecharPosicaoAsync(Arg.Any<string>(), Arg.Any<ulong>(), Arg.Any<CancellationToken>());

        // Ciclo 2, novo candle + retorna true
        _gateway.ObterCandlesAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(new List<CandleMt5> { new CandleMt5(timeBase2, 1, 2, 1, 2, 100) });
        
        estratSaidaObj.AvaliarSaida(Arg.Any<bool>(), Arg.Any<IReadOnlyList<CandleMt5>>(), Arg.Any<IReadOnlyList<(IndicadorConfig, ResultadoIndicador)>>(), Arg.Any<IReadOnlyList<(IndicadorConfig, ResultadoIndicador)>>(), Arg.Any<DetalhesPosicaoMt5>(), Arg.Any<Domain.Events.TickEvent>(), Arg.Any<EstrategiaConfig>())
            .Returns((true, "saida/venda")); 

        await (Task)method!.Invoke(sut, new object[] { CancellationToken.None })!;
        
        await _execucao.Received(1).FecharPosicaoAsync("T1", 12345, Arg.Any<CancellationToken>());
    }
}
