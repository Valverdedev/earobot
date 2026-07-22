using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Execution;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Execution;

public class CalculoSaidaParcialTests
{
    private static readonly (double MinVolume, double MaxVolume, double VolumeStep) RegrasVolumePadrao = (0.01, 100, 0.01);

    [Theory]
    [InlineData(true, 100000, 100200, 200)]
    [InlineData(false, 100000, 99800, 200)]
    public void CalcularLucroAtual_DeveRetornarLucroNaDirecaoCorreta(bool compra, double abertura, double atual, double esperado)
    {
        CalculoSaidaParcial.CalcularLucroAtual(compra, abertura, atual).Should().Be(esperado);
    }

    [Fact]
    public void CalcularVolumeParcial_PrimeiraSaidaDeTresContratosComDuasParciaisDeMetade_DeveFecharUmContrato()
    {
        var parciais = new List<SaidaParcialConfig>
        {
            new(0.5, 200), // primeira parcial: metade do volume original
            new(0.5, 400), // segunda parcial: metade do que sobrou
        };

        // Posição ainda com o volume original completo (3 contratos), nenhuma parcial executada ainda.
        var volume = CalculoSaidaParcial.CalcularVolumeParcial(3.0, parciais, indice: 0, RegrasVolumePadrao);

        volume.Should().Be(1.5);
    }

    [Fact]
    public void CalcularVolumeParcial_SegundaSaidaAposPrimeiraJaExecutada_DeveConsiderarVolumeRestante()
    {
        var parciais = new List<SaidaParcialConfig>
        {
            new(0.5, 200),
            new(0.5, 400),
        };

        // Após a 1ª parcial (50% de 4 contratos = 2 fechados), restam 2 contratos.
        var volume = CalculoSaidaParcial.CalcularVolumeParcial(2.0, parciais, indice: 1, RegrasVolumePadrao);

        // Segunda parcial-alvo = 50% do volume original (4) = 2 -> fecha o restante inteiro.
        volume.Should().Be(2.0);
    }

    [Fact]
    public void CalcularVolumeParcial_QuandoResultadoAbaixoDoLoteMinimo_DeveFecharVolumeInteiro()
    {
        var parciais = new List<SaidaParcialConfig> { new(0.1, 100) };
        var regras = (MinVolume: 0.01, MaxVolume: 100.0, VolumeStep: 0.01);

        // 10% de 0.01 normaliza para 0 (abaixo do lote mínimo) -> deve fechar tudo em vez de travar.
        var volume = CalculoSaidaParcial.CalcularVolumeParcial(0.01, parciais, indice: 0, regras);

        volume.Should().Be(0.01);
    }

    [Fact]
    public void CalcularBreakEvenAposParcial_PrimeiraParcial_DeveRetornarPrecoDeEntrada()
    {
        var parciais = new List<SaidaParcialConfig> { new(0.5, 200), new(0.5, 400) };

        var sl = CalculoSaidaParcial.CalcularBreakEvenAposParcial(true, 100000, parciais, indiceExecutado: 0);

        sl.Should().Be(100000);
    }

    [Fact]
    public void CalcularBreakEvenAposParcial_SegundaParcialCompra_DeveRetornarDistanciaDaParcialAnterior()
    {
        var parciais = new List<SaidaParcialConfig> { new(0.5, 200), new(0.5, 400) };

        var sl = CalculoSaidaParcial.CalcularBreakEvenAposParcial(true, 100000, parciais, indiceExecutado: 1);

        sl.Should().Be(100200);
    }

    [Fact]
    public void CalcularBreakEvenAposParcial_SegundaParcialVenda_DeveRetornarDistanciaAbaixoDaEntrada()
    {
        var parciais = new List<SaidaParcialConfig> { new(0.5, 200), new(0.5, 400) };

        var sl = CalculoSaidaParcial.CalcularBreakEvenAposParcial(false, 100000, parciais, indiceExecutado: 1);

        sl.Should().Be(99800);
    }
}
