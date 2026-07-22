using Financial.Robot.Worker.Execution;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Execution;

public class CalculoSaidaParametricaTests
{
    [Theory]
    [InlineData(true, 100.0, 1.0, true, 99.0)]    // Compra SL 1% -> 100 - 1 = 99
    [InlineData(true, 100.0, 1.0, false, 101.0)]  // Compra TP 1% -> 100 + 1 = 101
    [InlineData(false, 100.0, 1.0, true, 101.0)]   // Venda SL 1% -> 100 + 1 = 101
    [InlineData(false, 100.0, 1.0, false, 99.0)]   // Venda TP 1% -> 100 - 1 = 99
    public void CalcularPrecoPercentual_DeveRetornarPrecoCorreto(bool compra, double precoEntrada, decimal percentual, bool isStopLoss, double precoEsperado)
    {
        var preco = CalculoSaidaParametrica.CalcularPrecoPercentual(compra, precoEntrada, percentual, isStopLoss);
        preco.Should().BeApproximately(precoEsperado, 0.00001);
    }

    [Theory]
    [InlineData(100.0, 100.0, 150.0, true)] // Lucro bate no TP bruto
    [InlineData(99.0, 100.0, 150.0, false)] // Lucro abaixo do TP bruto
    [InlineData(-150.0, 100.0, 150.0, true)] // Prejuízo bate no SL bruto
    [InlineData(-100.0, 100.0, 150.0, false)] // Prejuízo menor que o SL bruto
    public void AtingiuLimiteValorBruto_DeveAvaliarCorretamente(
        double profit, double tpBruto, double slBruto, bool esperado)
    {
        var result = CalculoSaidaParametrica.AtingiuLimiteValorBruto(profit, (decimal?)tpBruto, (decimal?)slBruto, out _);
        result.Should().Be(esperado);
    }

    [Theory]
    [InlineData(-50.0, 1000.0, 10.0, 5.0, true)] 
    [InlineData(-49.0, 1000.0, 10.0, 5.0, false)] 
    [InlineData(100.0, 1000.0, 10.0, 5.0, true)] 
    [InlineData(99.0, 1000.0, 10.0, 5.0, false)] 
    public void AtingiuLimitePercentualConta_DeveAvaliarCorretamente(
        double profit, double saldo, double tpPct, double slPct, bool esperado)
    {
        // Act
        var result = CalculoSaidaParametrica.AtingiuLimitePercentualConta(profit, saldo, (decimal?)tpPct, (decimal?)slPct, out _);

        // Assert
        result.Should().Be(esperado);
    }

    [Fact]
    public void CalcularDistanciaTrailingPercentual_DeveRetornarValorCorreto()
    {
        var dist = CalculoSaidaParametrica.CalcularDistanciaTrailingPercentual(100000.0, 0.5m);
        dist.Should().BeApproximately(500.0, 0.00001); // 0.5% de 100000 = 500
    }
}
