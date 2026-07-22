using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.Services;
using FluentAssertions;

namespace Financial.Robot.Domain.Tests.Services;

/// <summary>Testes para o Domain Service ServicoCalculoPreco.</summary>
public sealed class ServicoCalculoPrecoTests
{
    private readonly ServicoCalculoPreco _servico = new();
    private const double TamanhoPontoPadrao = 0.00001; // EUR/USD 5 decimais

    [Fact]
    public void CalcularStopLoss_ComOrdemBuy_DeveSubtrairDistancia()
    {
        // Arrange
        double precoAbertura = 1.10000;
        int pips = 20;

        // Act
        var sl = _servico.CalcularStopLoss("BUY", precoAbertura, pips, TamanhoPontoPadrao);

        // Assert — 20 pips = 20 * 0.00001 * 10 = 0.002
        sl.Should().BeApproximately(1.09800, 0.000001);
    }

    [Fact]
    public void CalcularStopLoss_ComOrdemSell_DeveSomarDistancia()
    {
        // Arrange
        double precoAbertura = 1.10000;
        int pips = 20;

        // Act
        var sl = _servico.CalcularStopLoss("SELL", precoAbertura, pips, TamanhoPontoPadrao);

        // Assert
        sl.Should().BeApproximately(1.10200, 0.000001);
    }

    [Fact]
    public void CalcularTakeProfit_ComOrdemBuy_DeveSomarDistancia()
    {
        // Arrange
        double precoAbertura = 1.10000;
        int pips = 20;

        // Act
        var tp = _servico.CalcularTakeProfit("BUY", precoAbertura, pips, TamanhoPontoPadrao);

        // Assert
        tp.Should().BeApproximately(1.10200, 0.000001);
    }

    [Fact]
    public void CalcularPrecoOrdemPendente_ComBuyLimit_DeveSubtrairDistancia()
    {
        // Arrange
        double precoAtual = 1.10000;
        int distanciaPips = 50;

        // Act
        var preco = _servico.CalcularPrecoOrdemPendente(
            "BUY_LIMIT", precoAtual, distanciaPips, TamanhoPontoPadrao);

        // Assert — 50 pips = 0.005
        preco.Should().BeApproximately(1.09500, 0.000001);
    }

    [Theory]
    [InlineData(0.0, 20, 0.00001)]
    [InlineData(1.10000, 0, 0.00001)]
    [InlineData(1.10000, 20, 0.0)]
    public void CalcularStopLoss_ComParametrosInvalidos_DeveLancarDomainException(
        double preco, int pips, double tamanhoPonto)
    {
        // Arrange & Act
        var acao = () => _servico.CalcularStopLoss("BUY", preco, pips, tamanhoPonto);

        // Assert
        acao.Should().Throw<DomainException>();
    }
}
