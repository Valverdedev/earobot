using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Execution;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Execution;

public class ValidadorSaidaConfigTests
{
    [Fact]
    public void Validar_ComConfigValida_DeveRetornarVazio()
    {
        var config = new SaidaConfig(
            StopLossPercentualPreco: 1.0m,
            TakeProfitPercentualPreco: 2.0m,
            TrailingStopPercentualPreco: 0.5m
        );

        var erros = ValidadorSaidaConfig.Validar(config);

        erros.Should().BeEmpty();
    }

    [Fact]
    public void Validar_ComMultiplasFontesSl_DeveRetornarErro()
    {
        var config = new SaidaConfig(
            StopLossPips: 100,
            StopLossPercentualPreco: 1.0m
        );

        var erros = ValidadorSaidaConfig.Validar(config);

        erros.Should().ContainSingle(e => e.Contains("Múltiplas fontes de Stop Loss de servidor"));
    }

    [Fact]
    public void Validar_ComMultiplasFontesTp_DeveRetornarErro()
    {
        var config = new SaidaConfig(
            TakeProfitAtrMultiplo: 2.0m,
            TakeProfitPercentualPreco: 1.0m
        );

        var erros = ValidadorSaidaConfig.Validar(config);

        erros.Should().ContainSingle(e => e.Contains("Múltiplas fontes de Take Profit de servidor"));
    }

    [Fact]
    public void Validar_ComMultiplasFontesTrailing_DeveRetornarErro()
    {
        var config = new SaidaConfig(
            TrailingStopPips: 50,
            TrailingStopPercentualPreco: 0.5m
        );

        var erros = ValidadorSaidaConfig.Validar(config);

        erros.Should().ContainSingle(e => e.Contains("Múltiplas fontes de Trailing Stop configuradas"));
    }

    [Fact]
    public void Validar_ComValoresNegativos_DeveRetornarErro()
    {
        var config = new SaidaConfig(
            StopLossPercentualConta: -1.0m,
            TakeProfitValorBruto: 0m
        );

        var erros = ValidadorSaidaConfig.Validar(config);

        erros.Should().HaveCount(2);
        erros.Should().Contain(e => e.Contains("StopLossPercentualConta deve ser maior que zero"));
        erros.Should().Contain(e => e.Contains("TakeProfitValorBruto deve ser maior que zero"));
    }
}
