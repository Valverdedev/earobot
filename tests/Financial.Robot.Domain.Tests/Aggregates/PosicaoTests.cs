using Financial.Robot.Domain.Aggregates.Posicao;
using Financial.Robot.Domain.Enums;
using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.Excecoes;
using Financial.Robot.Domain.ValueObjects;
using FluentAssertions;

namespace Financial.Robot.Domain.Tests.Aggregates;

/// <summary>Testes para o Aggregate Posicao.</summary>
public sealed class PosicaoTests
{
    private static Posicao CriarPosicaoPadrao() => Posicao.Abrir(
        ticket: 12345UL,
        simbolo: new Simbolo("EURUSD"),
        tipoOrdem: TipoOrdem.Compra,
        volume: new Volume(0.01),
        precoAbertura: new PrecoMercado(1.10000),
        stopLoss: 1.09800,
        takeProfit: 1.10200);

    [Fact]
    public void Abrir_ComDadosValidos_DeveCriarPosicaoAberta()
    {
        // Act
        var posicao = CriarPosicaoPadrao();

        // Assert
        posicao.Id.Should().Be(12345UL);
        posicao.Status.Should().Be(StatusPosicao.Aberta);
        posicao.Simbolo.Valor.Should().Be("EURUSD");
        posicao.Volume.Valor.Should().Be(0.01);
        posicao.LucroPrejuizo.Should().BeNull();
    }

    [Fact]
    public void Abrir_DevPublicarPosicaoAbertaEvent()
    {
        // Act
        var posicao = CriarPosicaoPadrao();

        // Assert
        posicao.EventosDominio.Should().HaveCount(1);
        posicao.EventosDominio[0].Should().BeOfType<PosicaoAbertaEvent>();
    }

    [Fact]
    public void ModificarTakeProfit_ComPosicaoAberta_DeveAtualizarTakeProfit()
    {
        // Arrange
        var posicao = CriarPosicaoPadrao();
        posicao.LimparEventos();

        // Act
        posicao.ModificarTakeProfit(1.10300);

        // Assert
        posicao.TakeProfit.Should().Be(1.10300);
        posicao.EventosDominio.Should().HaveCount(1);
        posicao.EventosDominio[0].Should().BeOfType<PosicaoModificadaEvent>();
    }

    [Fact]
    public void ModificarTakeProfit_ComPosicaoFechada_DeveLancarDomainException()
    {
        // Arrange
        var posicao = CriarPosicaoPadrao();
        posicao.Fechar(1.10050, -0.50);

        // Act
        var acao = () => posicao.ModificarTakeProfit(1.10500);

        // Assert
        acao.Should().Throw<DomainException>()
            .WithMessage("*não está aberta*");
    }

    [Fact]
    public void Fechar_ComDadosValidos_DeveMudarStatusParaFechada()
    {
        // Arrange
        var posicao = CriarPosicaoPadrao();

        // Act
        posicao.Fechar(1.10050, 0.25);

        // Assert
        posicao.Status.Should().Be(StatusPosicao.Fechada);
        posicao.LucroPrejuizo.Should().Be(0.25);
        posicao.FechadaEm.Should().NotBeNull();
    }

    [Fact]
    public void LimparEventos_DeveRemoverTodosOsEventos()
    {
        // Arrange
        var posicao = CriarPosicaoPadrao();

        // Act
        posicao.LimparEventos();

        // Assert
        posicao.EventosDominio.Should().BeEmpty();
    }
}
