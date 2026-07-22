using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class HydrusEmaChannelBreakoutTests
{
    private readonly HydrusEmaChannelBreakout _sut;
    private readonly EstrategiaConfig _config;

    public HydrusEmaChannelBreakoutTests()
    {
        _sut = new HydrusEmaChannelBreakout();

        _config = new EstrategiaConfig("id", "Hydrus", 123, true, true, true, null, null, null, null, null, null)
        {
            Parametros = new Dictionary<string, object>
            {
                { "emaPeriodo", 156 },
                { "mediaCurtaPeriodo", 5 },
                { "canalPontos", 170.0 },
                { "spreadMaximoPontos", 10.0 },
                { "distanciaMaximaAposRompimentoPontos", 80.0 },
                { "usarConfirmacaoTimeframeMaior", false }
            }
        };
    }

    private List<CandleMt5> CriarHistoricoLong(int count, double basePrice = 100000)
    {
        var candles = new List<CandleMt5>();
        var time = DateTime.UtcNow.Date.AddDays(-5);
        for (int i = 0; i < count; i++)
        {
            candles.Add(new CandleMt5(time.AddMinutes(i), basePrice, basePrice + 10, basePrice - 10, basePrice, 1000));
        }
        return candles;
    }

    [Fact]
    public void Avaliar_HistoricoInsuficiente_RetornaAguardar()
    {
        var candles = CriarHistoricoLong(150); // Menos de 156+10
        var tick = new TickEvent("genial", "WINQ26", 100000, 100005, DateTime.UtcNow);
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Histórico insuficiente");
    }

    [Fact]
    public void Avaliar_SpreadAlto_RetornaAguardar()
    {
        var candles = CriarHistoricoLong(200);
        var tick = new TickEvent("genial", "WINQ26", 100000, 100015, DateTime.UtcNow); // Spread = 15
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Spread muito alto");
    }

    [Fact]
    public void Avaliar_CruzamentoAlta_RetornaComprar()
    {
        var candles = CriarHistoricoLong(200, 100000);
        // EMA será ~100000. Banda superior = 100170.
        
        // Para a SMMA(5) cruzar 100170 vindo de 100000, o preço atual precisaria ser muito alto
        // Vamos dar um "pico" absurdo no último candle para puxar a SMMA para > 100170
        // SMMA = (SMMA_prev * 4 + price) / 5. (100000 * 4 + 101000) / 5 = 100200
        candles[^2] = new CandleMt5(DateTime.UtcNow.AddMinutes(-2), 100100, 100100, 100100, 100100, 100);
        candles[^1] = new CandleMt5(DateTime.UtcNow.AddMinutes(-1), 101000, 101000, 101000, 101000, 100);

        var tick = new TickEvent("genial", "WINQ26", 100175, 100180, DateTime.UtcNow);
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeTrue();
        result.Lado.Should().Be(LadoOrdem.Compra);
        result.Motivo.Should().Contain("Cruzamento banda superior");
    }

    [Fact]
    public void Avaliar_CruzamentoBaixa_RetornaVender()
    {
        var candles = CriarHistoricoLong(200, 100000);
        // EMA será ~100000. Banda inferior = 99830.
        
        // SMMA_prev ~ 100000. Para SMMA cruzar 99830 para baixo: (400000 + P) / 5 < 99830
        // 400000 + P < 499150 -> P < 99150
        candles[^2] = new CandleMt5(DateTime.UtcNow.AddMinutes(-2), 99900, 99900, 99900, 99900, 100);
        candles[^1] = new CandleMt5(DateTime.UtcNow.AddMinutes(-1), 99000, 99000, 99000, 99000, 100);

        var tick = new TickEvent("genial", "WINQ26", 99760, 99765, DateTime.UtcNow);
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeTrue();
        result.Lado.Should().Be(LadoOrdem.Venda);
        result.Motivo.Should().Contain("Cruzamento banda inferior");
    }

    [Fact]
    public void Avaliar_CruzamentoBaixaMuitoDistante_RetornaAguardar()
    {
        var candles = CriarHistoricoLong(200, 100000);
        candles[^2] = new CandleMt5(DateTime.UtcNow.AddMinutes(-2), 99900, 99900, 99900, 99900, 100);
        candles[^1] = new CandleMt5(DateTime.UtcNow.AddMinutes(-1), 99000, 99000, 99000, 99000, 100);

        // Preço atual despencou para 99000. Distância da banda(99830) = 830 > 80(distMax)
        var tick = new TickEvent("genial", "WINQ26", 99000, 99005, DateTime.UtcNow);
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("muito longe da banda");
    }

    [Fact]
    public void Avaliar_MediaCurtaTipo_Sma_Sucesso()
    {
        _config.Parametros["mediaCurtaTipo"] = "SMA";
        var candles = CriarHistoricoLong(200, 100000);
        
        // Com SMA(5), a média reage mais rápido que SMMA
        candles[^2] = new CandleMt5(DateTime.UtcNow.AddMinutes(-2), 100100, 100100, 100100, 100100, 100);
        candles[^1] = new CandleMt5(DateTime.UtcNow.AddMinutes(-1), 101000, 101000, 101000, 101000, 100);

        var tick = new TickEvent("genial", "WINQ26", 100175, 100180, DateTime.UtcNow);
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeTrue();
        result.Motivo.Should().Contain("Cruzamento banda superior");
    }

    [Fact]
    public void Avaliar_M5Confirmacao_HistoricoInsuficiente_RetornaAguardar()
    {
        _config.Parametros["usarConfirmacaoTimeframeMaior"] = true;
        // emaConf(21) + 100 = 121 * 5 = 605 velas necessárias. Vamos mandar 600.
        var candles = CriarHistoricoLong(600, 100000);
        var tick = new TickEvent("genial", "WINQ26", 100000, 100005, DateTime.UtcNow);
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Histórico insuficiente para confirmação M5");
    }

    [Fact]
    public void Avaliar_M5Confirmacao_TendenciaContra_BloqueiaEntrada()
    {
        _config.Parametros["usarConfirmacaoTimeframeMaior"] = true;
        var candles = CriarHistoricoLong(650, 100000); // Suficiente para M5 (EMA será ~100000)
        
        // Gatilho de cruzamento de ALTA no M1
        candles[^2] = new CandleMt5(DateTime.UtcNow.AddMinutes(-2), 100100, 100100, 100100, 100100, 100);
        candles[^1] = new CandleMt5(DateTime.UtcNow.AddMinutes(-1), 101000, 101000, 101000, 101000, 100);

        var tick = new TickEvent("genial", "WINQ26", 99900, 99905, DateTime.UtcNow);
        
        var result = _sut.Avaliar(candles, new List<(IndicadorConfig, ResultadoIndicador)>(), new List<(IndicadorConfig, ResultadoIndicador)>(), tick, _config);
        
        // Deve falhar pois a confirmação exigia preço > EMA M5
        result.Executar.Should().BeFalse();
        result.Motivo.Should().Contain("Aguardando cruzamento das bandas");
    }

    [Fact]
    public void ObterLookbackNecessario_SemConfirmacao_RetornaEmaMaisMargem()
    {
        _config.Parametros["usarConfirmacaoTimeframeMaior"] = false;
        _config.Parametros["emaPeriodo"] = 156;
        
        var lookback = _sut.ObterLookbackNecessario(_config);
        
        lookback.Should().Be(166);
    }

    [Fact]
    public void ObterLookbackNecessario_ComM5_Retorna605()
    {
        _config.Parametros["usarConfirmacaoTimeframeMaior"] = true;
        _config.Parametros["emaPeriodo"] = 156;
        _config.Parametros["emaConfirmacaoPeriodo"] = 21;
        _config.Parametros["timeframeConfirmacao"] = "M5";
        
        var lookback = _sut.ObterLookbackNecessario(_config);
        
        lookback.Should().Be(605); // (21 + 100) * 5 = 605
    }

    [Fact]
    public void ObterLookbackNecessario_ComH1_Retorna7260()
    {
        _config.Parametros["usarConfirmacaoTimeframeMaior"] = true;
        _config.Parametros["emaPeriodo"] = 156;
        _config.Parametros["emaConfirmacaoPeriodo"] = 21;
        _config.Parametros["timeframeConfirmacao"] = "H1";
        
        var lookback = _sut.ObterLookbackNecessario(_config);
        
        lookback.Should().Be(7260); // (21 + 100) * 60 = 7260
    }

    [Fact]
    public void ObterLookbackNecessario_ComH4_Retorna29040()
    {
        _config.Parametros["usarConfirmacaoTimeframeMaior"] = true;
        _config.Parametros["emaPeriodo"] = 156;
        _config.Parametros["emaConfirmacaoPeriodo"] = 21;
        _config.Parametros["timeframeConfirmacao"] = "H4";
        
        var lookback = _sut.ObterLookbackNecessario(_config);
        
        lookback.Should().Be(29040); // (21 + 100) * 240 = 29040
    }
}
