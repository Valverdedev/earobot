using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Indicators;
using Financial.Robot.Worker.Strategy.Estrategias;
using FluentAssertions;
using Skender.Stock.Indicators;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class MicroTendenciaPullbackEmaTests
{
    private readonly MicroTendenciaPullbackEma _sut;
    private readonly EstrategiaConfig _configBase;

    public MicroTendenciaPullbackEmaTests()
    {
        _sut = new MicroTendenciaPullbackEma();

        var parametros = new Dictionary<string, object>
        {
            { "emaRapidaPeriodo", 9 },
            { "emaMediaPeriodo", 21 },
            { "emaLentaPeriodo", 50 },
            { "candlesInclinacao", 3 },
            { "toleranciaPullbackPontos", 40.0 },
            { "rompimentoMinimoPontos", 5.0 },
            { "spreadMaximoPontos", 15.0 },
            { "atrMinimoPontos", 10.0 },
            { "candleMaxAtrMultiplo", 10.0 }, // Folga para não bloquear testes genéricos
            { "rsiMaximoCompra", 70.0 },
            { "rsiMinimoVenda", 30.0 },
            { "usarFiltroVwap", false },
            { "exigirFechamentoDirecional", true }
        };

        _configBase = new EstrategiaConfig(
            "id", "MicroTendenciaPullbackEma", 999, true, true, true, 
            null, null, null, null, new List<IndicadorConfig>(), parametros);
    }

    private EstrategiaConfig CriarConfigComParametros(params (string Chave, object Valor)[] overrides)
    {
        var parametros = new Dictionary<string, object>(_configBase.Parametros!);
        foreach (var (chave, valor) in overrides)
            parametros[chave] = valor;

        return _configBase with { Parametros = parametros };
    }

    private (IReadOnlyList<CandleMt5> candles, TickEvent tick) ConfigurarCenarioTendenciaAlta(
        bool pullbackValido = true, bool spreadAlto = false, bool atrBaixo = false, bool candleGrande = false, 
        bool rsiAlto = false, bool vwapFalha = false, bool retomadaConfirma = true, bool alinhada = true)
    {
        var tempoBase = DateTime.UtcNow.AddMinutes(-300).Date.AddHours(10);
        var candles = new List<CandleMt5>();
        double close = 100000;

        for (int i = 0; i < 200; i++)
        {
            double open = close;
            // Tendência de alta
            if (alinhada)
                close = open + (i % 2 == 0 ? 15 : -10); // Net +5, RS = 1.5, RSI = 60
            else
                close = open + (i > 195 ? -50 : (i % 2 == 0 ? 15 : -10)); // Quebra EMA Rapida mas nao a Lenta

            double high = Math.Max(open, close) + (atrBaixo ? 2 : 20);
            double low = Math.Min(open, close) - (atrBaixo ? 2 : 20);

            if (rsiAlto && i > 180)
            {
                close = open + 50; // Força RSI para cima
                high = close + 10;
                low = open - 10;
            }

            double currentClose = close;
            long volume = 1000;
            if (vwapFalha && i == 0)
            {
                // Puxa a VWAP para cima artificialmente no início do dia
                high = 110000;
                low = 110000;
                currentClose = 110000;
                volume = 100000000;
            }
            candles.Add(new CandleMt5(tempoBase.AddMinutes(i), open, high, low, currentClose, volume));
        }

        // Ajustar o último candle (candles[^1]) com base nas EMAs reais para o pullback perfeito
        var quotes = candles.Take(199).Select(c => new Quote { Date = c.Tempo, Open = (decimal)c.Abertura, High = (decimal)c.Maximo, Low = (decimal)c.Minimo, Close = (decimal)c.Fechamento, Volume = c.Volume }).ToList();
        var emaR = (double)quotes.GetEma(9).Last().Ema!;
        var emaL = (double)quotes.GetEma(50).Last().Ema!;
        
        var prevCandle = candles[^2];
        var currentCandle = candles[^1];

        double newLow = emaR - 10; // Perfeito no pullback zone
        if (!pullbackValido)
            newLow = emaR + 100; // Longe do pullback

        double newClose = prevCandle.Maximo + 10; // Rompe a máxima anterior (gatilho)
        if (!retomadaConfirma)
            newClose = newLow; // Fecha na mínima (negativo)
            
        double newHigh = Math.Max(newClose, currentCandle.Maximo) + 10;

        if (candleGrande)
            newHigh = newLow + 500; // Cria um candle gigantesco que falhará no ATR múltiplo

        candles[^1] = new CandleMt5(currentCandle.Tempo, newLow + 5, newHigh, newLow, newClose, 1000);

        double tickBid = candles[^1].Fechamento;
        double tickAsk = tickBid + (spreadAlto ? 20 : 5);

        if (vwapFalha)
        {
            // O tickBid ficará na faixa de 101000.
            // A VWAP estará perto de 105000 devido ao candle de alto volume.
            // tickBid > atualLenta continua verdadeiro, mas tickBid < VWAP acionará o bloqueio.
        }

        var tick = new TickEvent("T1", "WIN", tickBid, tickAsk, candles[^1].Tempo.AddSeconds(30));
        return (candles, tick);
    }

    private (IReadOnlyList<CandleMt5> candles, TickEvent tick) ConfigurarCenarioTendenciaBaixa(
        bool pullbackValido = true, bool retomadaConfirma = true)
    {
        var tempoBase = DateTime.UtcNow.AddMinutes(-300).Date.AddHours(10);
        var candles = new List<CandleMt5>();
        double close = 100000;

        for (int i = 0; i < 200; i++)
        {
            double open = close;
            // Tendência de baixa
            close = open + (i % 2 == 0 ? -15 : 10); // Net -5, RS = 10/15 = 0.66, RSI = 40

            double high = Math.Max(open, close) + 20;
            double low = Math.Min(open, close) - 20;
            candles.Add(new CandleMt5(tempoBase.AddMinutes(i), open, high, low, close, 1000));
        }

        var quotes = candles.Take(199).Select(c => new Quote { Date = c.Tempo, Open = (decimal)c.Abertura, High = (decimal)c.Maximo, Low = (decimal)c.Minimo, Close = (decimal)c.Fechamento, Volume = c.Volume }).ToList();
        var emaR = (double)quotes.GetEma(9).Last().Ema!;
        var emaL = (double)quotes.GetEma(50).Last().Ema!;
        
        var prevCandle = candles[^2];
        var currentCandle = candles[^1];

        double newHigh = emaR + 10; 
        if (!pullbackValido)
            newHigh = emaR - 100; 

        double newClose = prevCandle.Minimo - 10; 
        if (!retomadaConfirma)
            newClose = prevCandle.Minimo + 10; 
            
        double newLow = Math.Min(newClose, currentCandle.Minimo) - 10;

        candles[^1] = new CandleMt5(currentCandle.Tempo, newHigh - 5, newHigh, newLow, newClose, 1000);

        double tickBid = candles[^1].Fechamento;
        double tickAsk = tickBid + 5;

        var tick = new TickEvent("T1", "WIN", tickBid, tickAsk, candles[^1].Tempo.AddSeconds(30));
        return (candles, tick);
    }

    [Fact]
    public void Compra_EmTendenciaCurta_ComPullbackValido()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta();
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, _configBase);
        resultado.Executar.Should().BeTrue(resultado.Motivo);
        resultado.Lado.Should().Be(LadoOrdem.Compra);
    }

    [Fact]
    public void Venda_EmTendenciaCurta_ComPullbackValido()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaBaixa();
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, _configBase);
        resultado.Executar.Should().BeTrue(resultado.Motivo);
        resultado.Lado.Should().Be(LadoOrdem.Venda);
    }

    [Fact]
    public void Bloqueio_EMAsDesalinhadas()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(alinhada: false);
        var config = CriarConfigComParametros(("rsiMaximoCompra", 101.0)); // Ignora RSI
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, config);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("EMAs não alinhadas em tendência");
    }

    [Fact]
    public void Bloqueio_RsiExtremo()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(rsiAlto: true);
        var config = CriarConfigComParametros(("rsiMaximoCompra", 68.0));
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, config);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("RSI");
    }

    [Fact]
    public void Bloqueio_SpreadAlto()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(spreadAlto: true);
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, _configBase);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("Spread atual");
    }

    [Fact]
    public void Bloqueio_AtrBaixo()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(atrBaixo: true);
        var config = CriarConfigComParametros(("atrMinimoPontos", 30.0));
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, config);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("ATR atual");
    }

    [Fact]
    public void Bloqueio_CandleGrandeDemais()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(candleGrande: true);
        var config = CriarConfigComParametros(("candleMaxAtrMultiplo", 1.2));
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, config);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("Candle atual muito grande");
    }

    [Fact]
    public void Bloqueio_FiltroVwap()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(vwapFalha: true);
        var config = CriarConfigComParametros(("usarFiltroVwap", true));
        
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, config);
        resultado.Executar.Should().BeFalse(resultado.Motivo);
        resultado.Motivo.Should().Contain("Preço abaixo da VWAP");
    }

    [Fact]
    public void SemSinal_NaoHouvePullback()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(pullbackValido: false);
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, _configBase);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("Pullback não alcançou");
    }

    [Fact]
    public void SemSinal_CandleRetomadaNaoConfirma()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta(retomadaConfirma: false);
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, _configBase);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("Aguardando candle de retomada fechar positivo");
    }
    [Fact]
    public void Bloqueio_HistoricoEmaInsuficiente()
    {
        var (candles, tick) = ConfigurarCenarioTendenciaAlta();
        // Com 51 candles, EmaLenta(50) terá apenas 2 itens na série,
        // falhando na condição EmaLentaSeries.Count < CandlesInclinacao + 1 (2 < 4).
        var candlesCurtos = candles.Take(51).ToList();
        var resultado = _sut.Avaliar(candlesCurtos, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, _configBase);
        resultado.Executar.Should().BeFalse();
        resultado.Motivo.Should().Contain("Histórico de indicadores insuficiente");
    }

    [Fact]
    public void Vwap_IgnoraCandlesDiasAnteriores()
    {
        var tempoBase = DateTime.UtcNow.AddDays(-1).Date.AddHours(10);
        var candles = new List<CandleMt5>();
        // Ontem: candle gigante que puxaria a VWAP lá pra cima se não fosse ancorada ao dia
        candles.Add(new CandleMt5(tempoBase, 110000, 110000, 110000, 110000, 100000000));
        
        // Hoje: candles normais na faixa de 100.000
        var tempoHoje = DateTime.UtcNow.Date.AddHours(10);
        double close = 100000;
        for (int i = 0; i < 200; i++)
        {
            double open = close;
            close = open + (i % 2 == 0 ? 15 : -10); 
            double high = Math.Max(open, close) + 20;
            double low = Math.Min(open, close) - 20;
            candles.Add(new CandleMt5(tempoHoje.AddMinutes(i), open, high, low, close, 1000));
        }
        
        var quotes = candles.Skip(1).Select(c => new Quote { Date = c.Tempo, Open = (decimal)c.Abertura, High = (decimal)c.Maximo, Low = (decimal)c.Minimo, Close = (decimal)c.Fechamento, Volume = c.Volume }).ToList();
        var emaR = (double)quotes.GetEma(9).Last().Ema!;
        var emaL = (double)quotes.GetEma(50).Last().Ema!;
        
        var prevCandle = candles[^2];
        var currentCandle = candles[^1];

        double newLow = emaR - 10;
        double newClose = prevCandle.Maximo + 10;
        double newHigh = Math.Max(newClose, currentCandle.Maximo) + 10;
        candles[^1] = new CandleMt5(currentCandle.Tempo, newLow + 5, newHigh, newLow, newClose, 1000);

        double tickBid = candles[^1].Fechamento;
        var tick = new TickEvent("T1", "WIN", tickBid, tickBid + 5, candles[^1].Tempo.AddSeconds(30));

        var config = CriarConfigComParametros(("usarFiltroVwap", true));
        var resultado = _sut.Avaliar(candles, Array.Empty<(IndicadorConfig, ResultadoIndicador)>(), null!, tick, config);
        
        // A VWAP deve ignorar o candle gigante de ontem e ancorar em hoje.
        resultado.Executar.Should().BeTrue(resultado.Motivo);
    }
}
