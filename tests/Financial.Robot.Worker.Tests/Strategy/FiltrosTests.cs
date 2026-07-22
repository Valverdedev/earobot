using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Worker.Strategy.Interpretada;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace Financial.Robot.Worker.Tests.Strategy;

public class FiltrosTests
{
    private static ContextoAvaliacao CriarContexto(CandleMt5[] candles, TickEvent tick)
    {
        var config = new EstrategiaConfig("1", "Teste", 1, true, true, true, null, null, null, null, null, null);
        return new ContextoAvaliacao(candles, tick, config);
    }

    private static CandleMt5[] GerarCandlesFixos(int quantidade, double range)
    {
        var lista = new List<CandleMt5>();
        var baseDate = new DateTime(2023, 1, 1);
        for(int i = 0; i < quantidade; i++)
        {
            // Open=100, High=100+range, Low=100, Close=100+(range/2)
            lista.Add(new CandleMt5(baseDate.AddMinutes(i), 100, 100 + range, 100, 100 + (range / 2), 1000));
        }
        return lista.ToArray();
    }

    [Fact]
    public void AvaliarFiltro_SpreadMaximo_DevePassarSeNoLimite()
    {
        var tick = new TickEvent("T1", "WIN", 95, 100, new DateTime(2023, 1, 1)); // spread 5
        var ctx = CriarContexto([], tick);
        var filtro = new FiltroDef { Tipo = "spreadMaximo", ValorPreco = 5 };

        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();
        var result = ((bool Passou, string Rastro))method!.Invoke(est, [filtro, ctx])!;

        result.Passou.Should().BeTrue();
    }

    [Fact]
    public void AvaliarFiltro_JanelaHorario_DevePassarDentroDaJanela()
    {
        var tick = new TickEvent("T1", "WIN", 95, 95, new DateTime(2023, 1, 1, 10, 30, 0));
        var ctx = CriarContexto([], tick);
        var filtro = new FiltroDef { Tipo = "janelaHorario", Inicio = "10:00", Fim = "11:00" };

        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();
        var result = ((bool Passou, string Rastro))method!.Invoke(est, [filtro, ctx])!;

        result.Passou.Should().BeTrue();
    }

    [Fact]
    public void AvaliarFiltro_JanelaHorario_DeveFalharForaDaJanela()
    {
        var tick = new TickEvent("T1", "WIN", 95, 95, new DateTime(2023, 1, 1, 11, 30, 0));
        var ctx = CriarContexto([], tick);
        var filtro = new FiltroDef { Tipo = "janelaHorario", Inicio = "10:00", Fim = "11:00" };

        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();
        var result = ((bool Passou, string Rastro))method!.Invoke(est, [filtro, ctx])!;

        result.Passou.Should().BeFalse();
    }

    [Fact]
    public void AvaliarFiltro_AtrMinimo_DevePassarOuFalhar()
    {
        // 10 candles com range de 20 para estabilizar o ATR(2)
        var candles = GerarCandlesFixos(10, 20);
        var ctx = CriarContexto(candles, new TickEvent("T1", "WIN", 95, 95, new DateTime()));
        
        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();

        // ATR de candles de range 20 (max 110 - min 90 = 20) é próximo de 20
        var filtroPassa = new FiltroDef { Tipo = "atrMinimo", Periodo = 2, ValorPreco = 10 };
        var resultPassa = ((bool Passou, string Rastro))method!.Invoke(est, [filtroPassa, ctx])!;
        resultPassa.Passou.Should().BeTrue();

        var filtroFalha = new FiltroDef { Tipo = "atrMinimo", Periodo = 2, ValorPreco = 30 };
        var resultFalha = ((bool Passou, string Rastro))method!.Invoke(est, [filtroFalha, ctx])!;
        resultFalha.Passou.Should().BeFalse();
    }

    [Fact]
    public void AvaliarFiltro_AtrMaximo_DevePassarOuFalhar()
    {
        var candles = GerarCandlesFixos(10, 20);
        var ctx = CriarContexto(candles, new TickEvent("T1", "WIN", 95, 95, new DateTime()));
        
        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();

        var filtroPassa = new FiltroDef { Tipo = "atrMaximo", Periodo = 2, ValorPreco = 30 };
        var resultPassa = ((bool Passou, string Rastro))method!.Invoke(est, [filtroPassa, ctx])!;
        resultPassa.Passou.Should().BeTrue();

        var filtroFalha = new FiltroDef { Tipo = "atrMaximo", Periodo = 2, ValorPreco = 10 };
        var resultFalha = ((bool Passou, string Rastro))method!.Invoke(est, [filtroFalha, ctx])!;
        resultFalha.Passou.Should().BeFalse();
    }

    [Fact]
    public void AvaliarFiltro_CandleMaxAtrMultiplo_DevePassarOuFalhar()
    {
        var candles = GerarCandlesFixos(10, 20).ToList();
        candles.Add(new CandleMt5(new DateTime(2023, 1, 1, 10, 0, 0), 100, 200, 100, 150, 1000)); // candle atual, range = 100
        var ctx = CriarContexto(candles.ToArray(), new TickEvent("T1", "WIN", 95, 95, new DateTime()));
        
        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();

        // O atr será próximo de 60 ( (20+100)/2 ), multi 1 => limit 60, candle atual 100 > 60 -> falha
        var filtroFalha = new FiltroDef { Tipo = "candleMaxAtrMultiplo", Periodo = 2, Multiplo = 1.0 };
        var resultFalha = ((bool Passou, string Rastro))method!.Invoke(est, [filtroFalha, ctx])!;
        resultFalha.Passou.Should().BeFalse();

        // multi 2 => limit 120, candle atual 100 <= 120 -> passa
        var filtroPassa = new FiltroDef { Tipo = "candleMaxAtrMultiplo", Periodo = 2, Multiplo = 2.0 };
        var resultPassa = ((bool Passou, string Rastro))method!.Invoke(est, [filtroPassa, ctx])!;
        resultPassa.Passou.Should().BeTrue();
    }

    [Fact]
    public void AvaliarFiltro_SemClimax_DevePassarOuFalhar()
    {
        var candles = new[]
        {
            new CandleMt5(new DateTime(2023, 1, 1, 10, 0, 0), 100, 110, 90, 100, 1000), // idx 1, range 20
            new CandleMt5(new DateTime(2023, 1, 1, 10, 5, 0), 100, 110, 90, 100, 1000), // idx 0, range 20
            new CandleMt5(new DateTime(2023, 1, 1, 10, 10, 0), 100, 200, 50, 100, 1000)  // candle atual, range 150
        };
        var ctx = CriarContexto(candles, new TickEvent("T1", "WIN", 95, 95, new DateTime()));
        
        var method = typeof(EstrategiaInterpretada).GetMethod("AvaliarFiltro", BindingFlags.NonPublic | BindingFlags.Instance);
        var est = new EstrategiaInterpretada();

        // Range medio(3) = (20+20+150)/3 = ~63
        // Limit = 63 * 1.5 = ~95. Candle atual tem range 150 -> Falha no Climax
        var filtroFalha = new FiltroDef { Tipo = "semClimax", LookbackMedioRange = 3, JanelaCandles = 1, MultiploRange = 1.5 };
        var resultFalha = ((bool Passou, string Rastro))method!.Invoke(est, [filtroFalha, ctx])!;
        resultFalha.Passou.Should().BeFalse();

        // Limit = 63 * 3 = 189. Candle atual tem range 150 -> Passa
        var filtroPassa = new FiltroDef { Tipo = "semClimax", LookbackMedioRange = 3, JanelaCandles = 1, MultiploRange = 3.0 };
        var resultPassa = ((bool Passou, string Rastro))method!.Invoke(est, [filtroPassa, ctx])!;
        resultPassa.Passou.Should().BeTrue();
    }
}
