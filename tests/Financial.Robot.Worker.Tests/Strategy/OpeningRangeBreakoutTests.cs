using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class OpeningRangeBreakoutTests
{
    private readonly OpeningRangeBreakout _sut;

    public OpeningRangeBreakoutTests()
    {
        _sut = new OpeningRangeBreakout();
    }

    [Theory]
    [InlineData("09:00-10:00", "10:00-17:00", "M1", 490)] // 8 horas = 480 min + 10
    [InlineData("09:00-10:00", "10:00-17:00", "M5", 106)] // 480 min / 5 = 96 + 10 = 106
    [InlineData("09:00-10:00", "10:00-17:00", "M15", 42)] // 480 / 15 = 32 + 10 = 42
    [InlineData("23:00-00:00", "00:00-04:00", "M1", 310)] // 5 horas = 300 + 10 (virada de dia)
    [InlineData("09:00-09:31", "09:31-09:31", "M5", 17)] // 31 min / 5 = 6.2 -> 7 + 10 = 17
    public void ObterLookbackNecessario_RetornaValorCorretoBaseadoNaDuracaoETimeframe(
        string janelaFormacao, string janelaOperacao, string timeframe, int lookbackEsperado)
    {
        var parametros = new Dictionary<string, object>
        {
            { "janelaFormacaoRange", janelaFormacao },
            { "janelaOperacao", janelaOperacao }
        };

        var indicadores = new List<IndicadorConfig>
        {
            new IndicadorConfig("Nenhum", timeframe, null)
        };

        var config = new EstrategiaConfig("id", "OpeningRangeBreakout", 999, true, true, true, null, null, null, null, indicadores, null)
        {
            Parametros = parametros
        };

        var lookback = _sut.ObterLookbackNecessario(config);

        lookback.Should().Be(lookbackEsperado);
    }

    [Fact]
    public void Avaliar_ReconstroiRangeCorretamenteQuandoAtravessaMeiaNoite()
    {
        var parametros = new Dictionary<string, object>
        {
            { "janelaFormacaoRange", "23:00-00:00" },
            { "janelaOperacao", "00:00-04:00" }
        };
        var config = new EstrategiaConfig("id", "OpeningRangeBreakout", 999, true, true, true, null, null, null, null, null, null)
        {
            Parametros = parametros
        };

        // Timestamp de hoje à 01:00 (dentro da operação)
        var hoje = DateTime.UtcNow.Date;
        var tempoReferencia = hoje.AddHours(1);
        var tick = new Financial.Robot.Domain.Events.TickEvent("T1", "WIN", 100050, 100060, tempoReferencia);

        // Candles da formação (das 23:00 de ONTEM)
        var ontem = hoje.AddDays(-1);
        var candles = new List<CandleMt5>
        {
            new CandleMt5(ontem.AddHours(23).AddMinutes(10), 100020, 100040, 100000, 100020, 1000),
            new CandleMt5(ontem.AddHours(23).AddMinutes(20), 100020, 100030, 100010, 100020, 1000)
        };

        // Range é 100000 - 100040.
        // Tick atual tem Ask = 100060 (> 100040). Então deve comprar.
        var resultado = _sut.Avaliar(candles, null!, null!, tick, config);

        resultado.Executar.Should().BeTrue();
        resultado.Lado.Should().Be(LadoOrdem.Compra);
    }
}
