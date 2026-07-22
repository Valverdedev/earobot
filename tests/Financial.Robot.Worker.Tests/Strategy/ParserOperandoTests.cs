using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class ParserOperandoTests
{
    [Fact]
    public void TryParse_DeveAnalisarCorretamenteIndicadorBasico()
    {
        var sucesso = ParserOperando.TryParse("ema(9)[1]", out var op, out var erro);
        sucesso.Should().BeTrue();
        erro.Should().BeNull();
        op!.Fonte.Should().Be(TipoFonte.Ema);
        op.Periodo.Should().Be(9);
        op.Indice.Should().Be(1);
        op.Modificador.Should().BeNull();
    }

    [Fact]
    public void TryParse_DeveAnalisarCorretamentePropriedadeTickComModificador()
    {
        var sucesso = ParserOperando.TryParse("tick.spread * 1.5", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.TickSpread);
        op.Modificador.Should().Be('*');
        op.ValorModificador.Should().Be(1.5);
    }

    [Fact]
    public void TryParse_DeveAnalisarCorretamentePropriedadeCandleComModificador()
    {
        var sucesso = ParserOperando.TryParse("candle[0].maximo - 10", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.CandleMaximo);
        op.Indice.Should().Be(0);
        op.Modificador.Should().Be('-');
        op.ValorModificador.Should().Be(10);
    }

    [Fact]
    public void TryParse_DeveRejeitarSintaxeInvalida()
    {
        var sucesso = ParserOperando.TryParse("tick.invalido *", out var op, out var erro);
        sucesso.Should().BeFalse();
        erro.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void TryParse_DeveAnalisarMediaPrecoMediano()
    {
        var sucesso = ParserOperando.TryParse("mediaPrecoMediano(SMMA, 5)", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.MediaPrecoMediano);
        op.ParametroString.Should().Be("SMMA");
        op.Periodo.Should().Be(5);
    }

    [Fact]
    public void TryParse_DeveAnalisarLiteral()
    {
        var sucesso = ParserOperando.TryParse("123.45", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.Literal);
        op.ValorLiteral.Should().Be(123.45);
    }
    [Fact]
    public void TryParse_DeveAnalisarCorretamentePosicaoPrecoEntrada()
    {
        var sucesso = ParserOperando.TryParse("posicao.precoEntrada", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.PosicaoPrecoEntrada);
    }

    [Fact]
    public void TryParse_DeveAnalisarCorretamentePosicaoLucroBruto()
    {
        var sucesso = ParserOperando.TryParse("posicao.lucroBruto", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.PosicaoLucroBruto);
    }

    [Fact]
    public void TryParse_DeveAnalisarCorretamentePosicaoLucroPercentualPreco()
    {
        var sucesso = ParserOperando.TryParse("posicao.lucroPercentualPreco", out var op, out var erro);
        sucesso.Should().BeTrue();
        op!.Fonte.Should().Be(TipoFonte.PosicaoLucroPercentualPreco);
    }

    [Fact]
    public void TryParse_DeveAnalisarFibonacciRetracao()
    {
        var sucesso = ParserOperando.TryParse("fibRet(0, 80, 0.618, \"baixa\")", out var op, out var erro);

        sucesso.Should().BeTrue();
        erro.Should().BeNull();
        op!.Fonte.Should().Be(TipoFonte.FibonacciRetracao);
        op.Periodo.Should().Be(0);
        op.Periodo2.Should().Be(80);
        op.NivelFibonacci.Should().Be(0.618);
        op.DirecaoFibonacci.Should().Be("baixa");
    }
}
