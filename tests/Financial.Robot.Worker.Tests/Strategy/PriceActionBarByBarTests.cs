using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class PriceActionBarByBarTests
{
    private readonly PriceActionBarByBar _sut = new();

    private static EstrategiaConfig ConfigBase(params (string Chave, object Valor)[] overrides)
    {
        var parametros = new Dictionary<string, object>
        {
            { "corpoMinimoFracaoRange", 0.55 },
            { "candlesImpulsoMinimo", 2 },
            { "candlesImpulsoMax", 4 },
            { "candlesPullbackMinimo", 1 },
            { "candlesPullbackMax", 3 },
            { "pullbackMaximoPercentual", 0.7 },
            { "lookbackMedioRange", 10 },
            { "climaxMultiploRange", 2.5 },
        };

        foreach (var (chave, valor) in overrides)
            parametros[chave] = valor;

        return new EstrategiaConfig(
            "id", "PriceActionBarByBar", 999, true, true, true,
            null, null, null, null, new List<IndicadorConfig>(), parametros);
    }

    private static CandleMt5 Candle(DateTime tempo, double abertura, double maximo, double minimo, double fechamento) =>
        new(tempo, abertura, maximo, minimo, fechamento, 100);

    private static TickEvent Tick(double bid, double ask) => new("terminal-teste", "WINQ26", bid, ask, DateTime.UtcNow);

    // Baseline neutro: candles pequenos e planos, sem trend bars, só para preencher o lookback do range médio.
    private static List<CandleMt5> BaseNeutra(DateTime inicio, int quantidade, double precoBase)
    {
        var candles = new List<CandleMt5>();
        var t = inicio;
        for (var i = 0; i < quantidade; i++)
        {
            candles.Add(Candle(t, precoBase, precoBase + 20, precoBase - 20, precoBase + 2));
            t = t.AddMinutes(1);
        }
        return candles;
    }

    [Fact]
    public void Avaliar_ImpulsoAltaPullbackERompimento_DeveComprar()
    {
        var t = DateTime.UtcNow.Date.AddHours(10);
        var candles = BaseNeutra(t, 15, 100000);
        t = candles[^1].Tempo.AddMinutes(1);

        // Impulso de alta: 3 trend bars fortes
        candles.Add(Candle(t, 100000, 100120, 99990, 100110)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100110, 100230, 100100, 100220)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100220, 100340, 100210, 100330)); t = t.AddMinutes(1);

        // Pullback controlado (barra de sinal = última, com máxima 100335)
        candles.Add(Candle(t, 100330, 100335, 100300, 100310)); t = t.AddMinutes(1);

        // Barra atual rompe a máxima da barra de sinal (100335) no fechamento
        candles.Add(Candle(t, 100310, 100360, 100305, 100350));

        var resultado = _sut.Avaliar(candles, [], [], Tick(100349, 100351), ConfigBase());

        resultado.Executar.Should().BeTrue();
        resultado.Lado.Should().Be(LadoOrdem.Compra);
    }

    [Fact]
    public void Avaliar_ImpulsoBaixaPullbackERompimento_DeveVender()
    {
        var t = DateTime.UtcNow.Date.AddHours(10);
        var candles = BaseNeutra(t, 15, 100000);
        t = candles[^1].Tempo.AddMinutes(1);

        // Impulso de baixa: 3 trend bars fortes
        candles.Add(Candle(t, 100000, 100010, 99880, 99890)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 99890, 99900, 99770, 99780)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 99780, 99790, 99660, 99670)); t = t.AddMinutes(1);

        // Pullback controlado (barra de sinal = última, com mínima 99665)
        candles.Add(Candle(t, 99670, 99700, 99665, 99690)); t = t.AddMinutes(1);

        // Barra atual rompe a mínima da barra de sinal (99665) no fechamento
        candles.Add(Candle(t, 99690, 99695, 99640, 99650));

        var resultado = _sut.Avaliar(candles, [], [], Tick(99649, 99651), ConfigBase());

        resultado.Executar.Should().BeTrue();
        resultado.Lado.Should().Be(LadoOrdem.Venda);
    }

    [Fact]
    public void Avaliar_SemPadrao_DeveAguardar()
    {
        var t = DateTime.UtcNow.Date.AddHours(10);
        var candles = BaseNeutra(t, 25, 100000);

        var resultado = _sut.Avaliar(candles, [], [], Tick(100000, 100002), ConfigBase());

        resultado.Executar.Should().BeFalse();
    }

    [Fact]
    public void Avaliar_BarraDeClimaxNoImpulso_DeveBloquearEntrada()
    {
        var t = DateTime.UtcNow.Date.AddHours(10);
        var candles = BaseNeutra(t, 15, 100000);
        t = candles[^1].Tempo.AddMinutes(1);

        // Impulso com uma barra de climax (range muito maior que o range médio ~40)
        candles.Add(Candle(t, 100000, 100120, 99990, 100110)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100110, 101500, 100100, 101490)); t = t.AddMinutes(1); // climax
        candles.Add(Candle(t, 101490, 101600, 101480, 101590)); t = t.AddMinutes(1);

        candles.Add(Candle(t, 101590, 101595, 101560, 101570)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 101570, 101620, 101565, 101610));

        var resultado = _sut.Avaliar(candles, [], [], Tick(101609, 101611), ConfigBase());

        resultado.Executar.Should().BeFalse();
    }

    [Fact]
    public void Avaliar_CompraDesabilitadaNaConfig_DeveAguardarMesmoComSinalValido()
    {
        var t = DateTime.UtcNow.Date.AddHours(10);
        var candles = BaseNeutra(t, 15, 100000);
        t = candles[^1].Tempo.AddMinutes(1);

        candles.Add(Candle(t, 100000, 100120, 99990, 100110)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100110, 100230, 100100, 100220)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100220, 100340, 100210, 100330)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100330, 100335, 100300, 100310)); t = t.AddMinutes(1);
        candles.Add(Candle(t, 100310, 100360, 100305, 100350));

        var config = ConfigBase() with { Comprar = false };

        var resultado = _sut.Avaliar(candles, [], [], Tick(100349, 100351), config);

        resultado.Executar.Should().BeFalse();
    }
}
