using Financial.Robot.Application.Backtesting.Services;
using Financial.Robot.Domain.ValueObjects;
using FluentAssertions;

namespace Financial.Robot.Application.Tests.Backtesting;

public class ExecutionSimulatorTests
{
    private readonly ExecutionSimulator _sut;

    public ExecutionSimulatorTests()
    {
        _sut = new ExecutionSimulator();
    }

    [Fact]
    public void Compra_CandleTocaTP_RealizaLucroEDeduzComissao()
    {
        // Arrange
        _sut.Initialize(capitalInicial: 1000.0, spreadFixo: 2.0, slippageFixo: 1.0, comissaoPorContrato: 5.0);

        var entryCandle = new CandleMt5(new DateTime(2026, 1, 1, 10, 0, 0), 100, 105, 95, 100, 1000);
        
        // Act - Entrada
        _sut.ExecuteOrder(LadoOrdem.Compra, volume: 10.0, stopLoss: 50.0, takeProfit: 150.0, entryCandle);

        // entryPrice = 100 + spread(2) + slippage(1) = 103

        // Act - Processo Candle bate TP (Maximo = 160)
        var tpCandle = new CandleMt5(new DateTime(2026, 1, 1, 10, 5, 0), 100, 160, 100, 150, 1000);
        _sut.ProcessCandle(tpCandle);

        var metrics = _sut.GetMetrics();

        // Assert
        metrics.TotalTrades.Should().Be(1);
        metrics.Trades.First().MotivoSaida.Should().Be("TakeProfit");
        
        // PnL Bruto: (150 (TP) - 1 (slippage) - 103 (entry)) * 10 (volume) = 46 * 10 = 460
        // PnL Líquido: 460 - (5.0 comissao * 10 volume) = 460 - 50 = 410
        metrics.LucroLiquidoTotal.Should().Be(410.0);
        metrics.CurvaEquity.Last().Equity.Should().Be(1410.0);
    }

    [Fact]
    public void Venda_CandleTocaSL_RealizaPrejuizo()
    {
        // Arrange
        _sut.Initialize(capitalInicial: 1000.0, spreadFixo: 0.0, slippageFixo: 0.0, comissaoPorContrato: 0.0);

        var entryCandle = new CandleMt5(new DateTime(2026, 1, 1, 10, 0, 0), 100, 105, 95, 100, 1000);
        
        // Act - Entrada
        _sut.ExecuteOrder(LadoOrdem.Venda, volume: 1.0, stopLoss: 120.0, takeProfit: 50.0, entryCandle);
        // entryPrice = 100

        // Act - Bate SL
        var slCandle = new CandleMt5(new DateTime(2026, 1, 1, 10, 5, 0), 100, 130, 95, 125, 1000);
        _sut.ProcessCandle(slCandle);

        var metrics = _sut.GetMetrics();

        // Assert
        metrics.TotalTrades.Should().Be(1);
        metrics.Trades.First().MotivoSaida.Should().Be("StopLoss");
        metrics.LucroLiquidoTotal.Should().Be(-20.0); // (100 - 120) * 1
    }

    [Fact]
    public void ColisaoSLTP_NoMesmoCandle_ResolvePeloStopLoss_PoliticaConservadora()
    {
        // Arrange
        _sut.Initialize(capitalInicial: 1000.0, spreadFixo: 0.0, slippageFixo: 0.0, comissaoPorContrato: 0.0);
        var entryCandle = new CandleMt5(new DateTime(2026, 1, 1, 10, 0, 0), 100, 105, 95, 100, 1000);
        _sut.ExecuteOrder(LadoOrdem.Compra, volume: 1.0, stopLoss: 80.0, takeProfit: 120.0, entryCandle);

        // Act - Candle maluco toca os dois!
        var collisionCandle = new CandleMt5(new DateTime(2026, 1, 1, 10, 5, 0), 100, 130, 70, 100, 1000);
        _sut.ProcessCandle(collisionCandle);

        var metrics = _sut.GetMetrics();

        // Assert
        metrics.TotalTrades.Should().Be(1);
        metrics.Trades.First().MotivoSaida.Should().Be("StopLoss_ConservativeCollision");
        metrics.LucroLiquidoTotal.Should().Be(-20.0); // Bateu 80, perdeu 20.
    }
}
