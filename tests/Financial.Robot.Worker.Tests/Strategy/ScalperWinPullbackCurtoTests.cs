using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class ScalperWinPullbackCurtoTests
{
    private readonly ScalperWinPullbackCurto _sut;
    private readonly EstrategiaConfig _config;

    public ScalperWinPullbackCurtoTests()
    {
        _sut = new ScalperWinPullbackCurto();

        var niveisJson = JsonSerializer.SerializeToElement(new[]
        {
            new { tipo = "resistencia", preco = 178150.0 },
            new { tipo = "suporte", preco = 177975.0 }
        });

        _config = new EstrategiaConfig("id", "ScalperWinPullbackCurto", 950, true, true, true, null, null, null, null, null, null)
        {
            Parametros = new Dictionary<string, object>
            {
                { "niveis", niveisJson },
                { "toleranciaNivelPontos", 35.0 },
                { "spreadMaximoPontos", 10.0 },
                { "distanciaMinimaVwapPontos", 40.0 },
                { "rsiMaximoVenda", 55.0 },
                { "rsiMinimoCompra", 40.0 }
            }
        };
    }

    [Fact]
    public void Avaliar_HistoricoInsuficiente_RetornaAguardar()
    {
        var candles = CriarHistorico(104);
        var tick = new TickEvent("genial", "WINQ26", 100, 105, DateTime.UtcNow);

        var result = _sut.Avaliar(candles, [], [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Historico insuficiente");
    }

    [Fact]
    public void Avaliar_AusenciaDeNiveis_RetornaAguardar()
    {
        var configSemNiveis = _config with { Parametros = new Dictionary<string, object>() };
        var candles = CriarHistorico(105);
        var tick = new TickEvent("genial", "WINQ26", 100, 105, DateTime.UtcNow);

        var result = _sut.Avaliar(candles, [], [], tick, configSemNiveis);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Nenhum nivel");
    }

    [Fact]
    public void Avaliar_SpreadAlto_RetornaAguardar()
    {
        var candles = CriarHistorico(105);
        var tick = new TickEvent("genial", "WINQ26", 178000, 178015, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(178000, 50, 178000, 178150, 178160);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Spread muito alto");
    }

    [Fact]
    public void Avaliar_ProximoVwap_RetornaAguardar()
    {
        var candles = CriarHistorico(105);
        var tick = new TickEvent("genial", "WINQ26", 178000, 178005, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(178000, 50, 178020, 178150, 178160);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Preco muito proximo da VWAP");
    }

    [Fact]
    public void Avaliar_PinbarVendedorComSombraDuasVezesCorpo_RetornaVender()
    {
        var candles = CriarHistorico(105, 178100);
        candles[^1] = new CandleMt5(DateTime.UtcNow, 178130, 178160, 178110, 178120, 1000);

        var tick = new TickEvent("genial", "WINQ26", 178100, 178105, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(178130, 50, 178000, 178140, 178150);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeTrue();
        result.Lado.Should().Be(LadoOrdem.Venda);
    }

    [Fact]
    public void Avaliar_CandleComCorpoGrandeSemPinbar_RetornaAguardar()
    {
        var candles = CriarHistorico(105, 178100);
        candles[^1] = new CandleMt5(DateTime.UtcNow, 178160, 178175, 178110, 178120, 1000);

        var tick = new TickEvent("genial", "WINQ26", 178100, 178105, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(178130, 50, 178000, 178140, 178150);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Condicoes de pullback");
    }

    [Fact]
    public void Avaliar_VendaRsiNaoConfirma_RetornaAguardar()
    {
        var candles = CriarHistorico(105, 178100);
        candles[^1] = new CandleMt5(DateTime.UtcNow, 178130, 178160, 178110, 178120, 1000);

        var tick = new TickEvent("genial", "WINQ26", 178100, 178105, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(178130, 60, 178000, 178140, 178150);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("acima do limite para venda");
    }

    [Fact]
    public void Avaliar_VendaM5NaoConfirmaQuandoPrecoNaoEstaAbaixoDasDuasEmas_RetornaAguardar()
    {
        var candles = CriarHistorico(105, 178100);
        candles[^1] = new CandleMt5(DateTime.UtcNow, 178130, 178160, 178110, 178120, 1000);

        var tick = new TickEvent("genial", "WINQ26", 178100, 178105, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(178130, 50, 178000, 178090, 178150);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("M5 nao confirma a venda");
    }

    [Fact]
    public void Avaliar_PinbarCompradorComSombraDuasVezesCorpo_RetornaComprar()
    {
        var candles = CriarHistorico(105, 177975);
        candles[^1] = new CandleMt5(DateTime.UtcNow, 177990, 178010, 177960, 178000, 1000);

        var tick = new TickEvent("genial", "WINQ26", 178020, 178025, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(177990, 45, 178100, 177980, 177990);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeTrue();
        result.Lado.Should().Be(LadoOrdem.Compra);
    }

    [Fact]
    public void Avaliar_CompraM5NaoConfirmaQuandoPrecoNaoEstaAcimaDasDuasEmas_RetornaAguardar()
    {
        var candles = CriarHistorico(105, 177975);
        candles[^1] = new CandleMt5(DateTime.UtcNow, 177990, 178010, 177960, 178000, 1000);

        var tick = new TickEvent("genial", "WINQ26", 178020, 178025, DateTime.UtcNow);
        var indicadores = CriarIndicadoresBase(177990, 45, 178100, 177980, 178030);

        var result = _sut.Avaliar(candles, indicadores, [], tick, _config);

        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("M5 nao confirma a compra");
    }

    private static List<CandleMt5> CriarHistorico(int count, double basePrice = 178000)
    {
        var candles = new List<CandleMt5>();
        var time = DateTime.UtcNow.Date.AddHours(10);

        for (var i = 0; i < count; i++)
            candles.Add(new CandleMt5(time.AddMinutes(i), basePrice, basePrice + 100, basePrice - 100, basePrice, 1000));

        return candles;
    }

    private static List<(IndicadorConfig, ResultadoIndicador)> CriarIndicadoresBase(
        double ema9M1,
        double rsiM1,
        double vwapM1,
        double ema9M5,
        double ema21M5)
    {
        return
        [
            (new IndicadorConfig("EMA", "M1", new Dictionary<string, object> { { "periodo", 9 } }), new ResultadoIndicador(DateTime.UtcNow, ema9M1)),
            (new IndicadorConfig("RSI", "M1", null), new ResultadoIndicador(DateTime.UtcNow, rsiM1)),
            (new IndicadorConfig("VWAP", "M1", null), new ResultadoIndicador(DateTime.UtcNow, vwapM1)),
            (new IndicadorConfig("EMA", "M5", new Dictionary<string, object> { { "periodo", 9 } }), new ResultadoIndicador(DateTime.UtcNow, ema9M5)),
            (new IndicadorConfig("EMA", "M5", new Dictionary<string, object> { { "periodo", 21 } }), new ResultadoIndicador(DateTime.UtcNow, ema21M5))
        ];
    }

    [Fact]
    public void ObterLookbackNecessario_DeveRetornar105()
    {
        _sut.ObterLookbackNecessario(_config).Should().Be(105);
    }
}
