using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.ValueObjects;
using FluentAssertions;

namespace Financial.Robot.Domain.Tests.ValueObjects;

/// <summary>Testes para o Value Object Pips.</summary>
public sealed class PipsTests
{
    [Fact]
    public void CriarPips_ComValorPositivo_DeveCriarComSucesso()
    {
        // Arrange & Act
        var pips = new Pips(20);

        // Assert
        pips.Valor.Should().Be(20);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-50)]
    public void CriarPips_ComValorInvalido_DeveLancarDomainException(int valorInvalido)
    {
        // Arrange & Act
        var acao = () => new Pips(valorInvalido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public void Pips_ConversaoImplicita_DeveRetornarInt()
    {
        // Arrange
        var pips = new Pips(30);

        // Act
        int valor = pips;

        // Assert
        valor.Should().Be(30);
    }

    [Fact]
    public void Pips_ToString_DeveIncluirUnidade()
    {
        // Arrange
        var pips = new Pips(20);

        // Assert
        pips.ToString().Should().Be("20 pips");
    }
}
