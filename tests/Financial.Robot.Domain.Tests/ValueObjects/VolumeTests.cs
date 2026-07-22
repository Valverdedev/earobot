using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.ValueObjects;
using FluentAssertions;

namespace Financial.Robot.Domain.Tests.ValueObjects;

/// <summary>Testes para o Value Object Volume.</summary>
public sealed class VolumeTests
{
    [Fact]
    public void CriarVolume_ComValorPositivo_DeveCriarComSucesso()
    {
        // Arrange & Act
        var volume = new Volume(0.01);

        // Assert
        volume.Valor.Should().Be(0.01);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(-1.0)]
    [InlineData(-100.0)]
    public void CriarVolume_ComValorInvalido_DeveLancarDomainException(double valorInvalido)
    {
        // Arrange & Act
        var acao = () => new Volume(valorInvalido);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*maior que zero*");
    }

    [Fact]
    public void Volume_ConversaoImplicita_DeveRetornarDouble()
    {
        // Arrange
        var volume = new Volume(1.5);

        // Act
        double valor = volume;

        // Assert
        valor.Should().Be(1.5);
    }

    [Fact]
    public void Volume_ComMesmoValor_DeveSerIgual()
    {
        // Arrange
        var volume1 = new Volume(0.01);
        var volume2 = new Volume(0.01);

        // Assert
        volume1.Should().Be(volume2);
    }
}
