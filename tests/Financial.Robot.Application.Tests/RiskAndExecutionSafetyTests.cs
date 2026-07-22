using Financial.Robot.Application.Interfaces;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Execution;
using Financial.Robot.Worker.Risk;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Financial.Robot.Application.Tests;

public sealed class RiskAndExecutionSafetyTests
{
    [Fact]
    public async Task CalcularAsync_ComModoPercentualConta_DeveBloquearCalculoSemMetadadosDoContrato()
    {
        var gateway = Substitute.For<IGatewayMt5>();
        var calculadora = new CalculadoraLote(NullLogger<CalculadoraLote>.Instance);

        var acao = async () => await calculadora.CalcularAsync(
            "percentualConta",
            loteFixo: null,
            percentualConta: 1,
            equity: 1_000,
            precoAtual: 100,
            distanciaStop: 2,
            gateway,
            "XAUUSDz");

        await acao.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Modo percentualConta bloqueado*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData(0.0)]
    public async Task AbrirPosicaoAsync_SemStopLossValido_DeveBloquearAntesDoGateway(double? stopLoss)
    {
        var connectionManager = Substitute.For<IConnectionManager>();
        var servico = new ServicoExecucao(connectionManager, NullLogger<ServicoExecucao>.Instance);

        var resultado = await servico.AbrirPosicaoAsync(
            "terminal",
            "EURUSD",
            LadoOrdem.Compra,
            volume: 0.01,
            sl: stopLoss,
            tp: 1.2);

        resultado.Sucesso.Should().BeFalse();
        resultado.Motivo.Should().Contain("Stop Loss");
        connectionManager.DidNotReceive().GetClient(Arg.Any<string>());
    }
}
