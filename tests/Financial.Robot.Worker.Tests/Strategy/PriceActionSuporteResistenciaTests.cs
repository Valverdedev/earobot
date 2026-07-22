using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class PriceActionSuporteResistenciaTests
{
    private readonly PriceActionSuporteResistencia _sut;

    public PriceActionSuporteResistenciaTests()
    {
        _sut = new PriceActionSuporteResistencia();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Avaliar_ComJsonElementBoolExigeReteste_DeveRespeitarValorBooleano(bool exigeReteste)
    {
        var niveisJson = JsonSerializer.SerializeToElement(new[]
        {
            new { tipo = "resistencia", preco = 1000.0 }
        });
        
        var configExigeReteste = JsonSerializer.SerializeToElement(exigeReteste);

        var config = new EstrategiaConfig("id", "PriceActionSuporteResistencia", 999, true, true, true, null, null, null, null, null, null)
        {
            Parametros = new Dictionary<string, object>
            {
                { "niveis", niveisJson },
                { "exigeRetesteConfirmado", configExigeReteste },
                { "toleranciaRompimentoPontos", 10.0 }
            }
        };

        // Rejeição de alta: pavio superior longo, fecha abaixo da abertura, testou o nivel de 1000.
        // Tolerância 10, nível 1000. Candle chega a 1005 (pavio longo) e fecha em 980 (abriu em 995).
        var candles = new List<CandleMt5>
        {
            new(DateTime.UtcNow.AddMinutes(-4), 980, 990, 970, 985, 100),
            new(DateTime.UtcNow.AddMinutes(-3), 985, 995, 980, 990, 100),
            new(DateTime.UtcNow.AddMinutes(-2), 990, 995, 985, 990, 100),
            new(DateTime.UtcNow.AddMinutes(-1), 990, 995, 980, 990, 100), 
            new(DateTime.UtcNow, 990, 1010, 970, 980, 100) // Rejeição clara (pavio superior = 20, corpo = 10)
        };
        
        var tick = new TickEvent("genial", "WIN", 980, 981, DateTime.UtcNow);

        // Act
        var result = _sut.Avaliar(candles, [], [], tick, config);

        // Assert
        if (exigeReteste)
        {
            // Se exige reteste, ignorar a rejeição e aguardar.
            result.Executar.Should().BeFalse();
        }
        else
        {
            // Se NÃO exige reteste, a rejeição é suficiente para vender.
            result.Executar.Should().BeTrue();
            result.Lado.Should().Be(LadoOrdem.Venda);
            result.Motivo.Should().Contain("rejeição de alta");
        }
    }
}
