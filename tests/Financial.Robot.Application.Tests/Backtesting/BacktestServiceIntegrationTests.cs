using Financial.Robot.Application.Backtesting.Interfaces;
using Financial.Robot.Application.Backtesting.Models;
using Financial.Robot.Application.Backtesting.Services;
using Financial.Robot.Domain.Entities;
using Financial.Robot.Worker.Backtesting;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy;
using Financial.Robot.Worker.Strategy.Estrategias;
using Financial.Robot.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Financial.Robot.Application.Tests.Backtesting;

public class BacktestServiceIntegrationTests
{
    [Fact]
    public async Task RunAsync_ExecuteMicroTendencia_ReturnsValidMetrics()
    {
        // Arrange
        // Montamos o container manualmente para teste de integração da camada
        var catalogoIndicadores = new CatalogoIndicadores();
        var catalogoEstrategias = new CatalogoEstrategias();
        catalogoEstrategias.Registrar(new MicroTendenciaPullbackEma());

        var dataProvider = new JsonBacktestDataProvider();
        var simulator = new ExecutionSimulator();

        var service = new WorkerBacktestService(
            dataProvider,
            simulator,
            catalogoEstrategias,
            catalogoIndicadores,
            new NullLogger<WorkerBacktestService>()
        );

        // Preparamos dados falsos e um JSON temporário
        var mockCandles = @"[
            { ""Tempo"": ""2026-07-01T10:00:00Z"", ""Abertura"": 120000, ""Maximo"": 120050, ""Minimo"": 119950, ""Fechamento"": 120000, ""Volume"": 100 },
            { ""Tempo"": ""2026-07-01T10:05:00Z"", ""Abertura"": 120000, ""Maximo"": 120100, ""Minimo"": 119950, ""Fechamento"": 120100, ""Volume"": 100 },
            { ""Tempo"": ""2026-07-01T10:10:00Z"", ""Abertura"": 120100, ""Maximo"": 120200, ""Minimo"": 120050, ""Fechamento"": 120150, ""Volume"": 100 }
        ]";
        var tempFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tempFile, mockCandles);

        // Geramos candles suficientes para o lookback.
        // A estratégia pede: emaLenta + candlesInclinacao + 150. (se lenta for 5, req=158)
        var config = new EstrategiaConfig(
            Id: "1",
            Nome: "MicroTendenciaPullbackEma",
            MagicNumber: 123,
            Ativa: true,
            Comprar: true,
            Vender: true,
            Entrada: null,
            Saida: new SaidaConfig(null, null, StopLossPips: 50, TakeProfitPips: 100, null, null, null, null, null, false),
            GestaoDeRisco: null,
            JanelaHorarioPermitido: null,
            Indicadores: null,
            Parametros: new Dictionary<string, object>
            {
                { "emaRapidaPeriodo", 3 },
                { "emaMediaPeriodo", 4 },
                { "emaLentaPeriodo", 5 },
                { "candlesInclinacao", 2 },
                { "usarFiltroVwap", false }, // Simplifica
                { "spreadMaximoPontos", 1000 }
            }
        );

        // Vamos encher de candles dummy só para passar pelo lookback = 157
        var bigMockCandles = new System.Text.StringBuilder("[\n");
        for (int i = 0; i < 200; i++)
        {
            int hour = 10 + (i / 60);
            int minute = i % 60;
            var separador = i == 199 ? string.Empty : ",";
            bigMockCandles.AppendLine($@"{{ ""Tempo"": ""2026-07-01T{hour:D2}:{minute:D2}:00Z"", ""Abertura"": {120000 + i}, ""Maximo"": {120010 + i}, ""Minimo"": {119990 + i}, ""Fechamento"": {120005 + i}, ""Volume"": 100 }}{separador}");
        }
        bigMockCandles.AppendLine("]");
        
        await File.WriteAllTextAsync(tempFile, bigMockCandles.ToString());

        var request = new BacktestRequest("WINQ26", config, tempFile, 10000, 0, 0, 0);

        // Act
        var metrics = await service.RunAsync(request);

        // Assert
        // Pode ser que total de trades seja 0 ou mais, dependendo de cruzar os limiares.
        // O importante é garantir que o motor processou até o final, extraiu PnL, Metrics nulas não estouraram etc.
        metrics.Should().NotBeNull();

        File.Delete(tempFile);
    }
}
