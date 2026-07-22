using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class EstrategiaInterpretadaTests
{
    [Fact]
    public void EstrategiaInterpretada_FluxoVazio_DeveAguardarPorFaltaDeConfig()
    {
        var est = new EstrategiaInterpretada();
        var config = new EstrategiaConfig("1", "Teste", 1, true, true, true, null, null, null, null, null, null);
        
        var res = est.Avaliar([], [], [], new TickEvent("T1", "WIN", 95, 95, new DateTime()), config);
        res.Executar.Should().BeFalse();
        res.Motivo.Should().Contain("não configurado");
    }

    [Fact]
    public void EstrategiaInterpretada_CacheInstancia_DeveEstarResolvido()
    {
        var est1 = new EstrategiaInterpretada();
        var est2 = new EstrategiaInterpretada();

        var config = new EstrategiaConfig("1", "Teste", 1, true, true, true, null, null, null, null, null, null);
        
        var lb1 = est1.ObterLookbackNecessario(config);
        var lb2 = est2.ObterLookbackNecessario(config);

        lb1.Should().Be(0);
        lb2.Should().Be(0);
    }
}
