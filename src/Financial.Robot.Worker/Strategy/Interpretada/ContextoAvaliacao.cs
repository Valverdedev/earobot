using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Domain.Interfaces;
using Skender.Stock.Indicators;
using System.Collections.Concurrent;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public sealed class ContextoAvaliacao
{
    private readonly IReadOnlyList<CandleMt5> _candles;
    private readonly TickEvent _tick;
    private readonly EstrategiaConfig _config;
    
    public DetalhesPosicaoMt5? PosicaoAtiva { get; }
    
    private readonly Lazy<List<Quote>> _quotes;
    private readonly Lazy<List<Quote>> _quotesMedian;
    
    private readonly ConcurrentDictionary<string, double?[]> _cacheIndicadores = new();
    
    private readonly ConcurrentDictionary<string, Lazy<List<Quote>>> _quotesTimeframe = new();
    private readonly ConcurrentDictionary<string, Lazy<List<Quote>>> _quotesMedianTimeframe = new();
    
    // Suporte a Padrões Compostos (Fase 2)
    private readonly Dictionary<string, CandleMt5> _refs = new();

    public void LimparRefs() => _refs.Clear();
    public void RegistrarRef(string nome, CandleMt5 candle) => _refs[nome] = candle;
    public CandleMt5? ObterRef(string nome) => _refs.TryGetValue(nome, out var c) ? c : null;

    public ContextoAvaliacao(IReadOnlyList<CandleMt5> candles, TickEvent tick, EstrategiaConfig config, DetalhesPosicaoMt5? posicaoAtiva = null)
    {
        _candles = candles;
        _tick = tick;
        _config = config;
        PosicaoAtiva = posicaoAtiva;

        _quotes = new Lazy<List<Quote>>(() => _candles.Select(c => new Quote
        {
            Date = c.Tempo,
            Open = (decimal)c.Abertura,
            High = (decimal)c.Maximo,
            Low = (decimal)c.Minimo,
            Close = (decimal)c.Fechamento,
            Volume = c.Volume
        }).OrderBy(q => q.Date).ToList());

        _quotesMedian = new Lazy<List<Quote>>(() => _candles.Select(c => new Quote
        {
            Date = c.Tempo,
            Open = (decimal)((c.Maximo + c.Minimo) / 2),
            High = (decimal)((c.Maximo + c.Minimo) / 2),
            Low = (decimal)((c.Maximo + c.Minimo) / 2),
            Close = (decimal)((c.Maximo + c.Minimo) / 2),
            Volume = c.Volume
        }).OrderBy(q => q.Date).ToList());
    }

    public double? ResolverOperando(Operando op)
    {
        var val = ObterValorBase(op);
        if (val is null) return null;

        if (op.Modificador.HasValue && op.ValorModificador.HasValue)
        {
            val = op.Modificador.Value switch
            {
                '+' => val + op.ValorModificador.Value,
                '-' => val - op.ValorModificador.Value,
                '*' => val * op.ValorModificador.Value,
                _ => val
            };
        }

        return val;
    }

    public CandleMt5? ObterCandle(int indice)
    {
        if (indice < 0 || indice >= _candles.Count) return null;
        return _candles[_candles.Count - 1 - indice];
    }

    private List<Quote> ObterQuotesParaTimeframe(string? timeframe)
    {
        if (string.IsNullOrEmpty(timeframe)) return _quotes.Value;

        var upperTf = timeframe.ToUpperInvariant();
        return _quotesTimeframe.GetOrAdd(upperTf, tf => new Lazy<List<Quote>>(() => 
            Financial.Robot.Worker.Indicators.AgregadorTimeframe.AgruparM1ParaTimeframeMaior(_candles, tf)
        )).Value;
    }

    private List<Quote> ObterQuotesMedianParaTimeframe(string? timeframe)
    {
        if (string.IsNullOrEmpty(timeframe)) return _quotesMedian.Value;

        var upperTf = timeframe.ToUpperInvariant();
        return _quotesMedianTimeframe.GetOrAdd(upperTf, tf => new Lazy<List<Quote>>(() => 
        {
            var agrupado = Financial.Robot.Worker.Indicators.AgregadorTimeframe.AgruparM1ParaTimeframeMaior(_candles, tf);
            return agrupado.Select(q => new Quote
            {
                Date = q.Date,
                Open = (q.High + q.Low) / 2,
                High = (q.High + q.Low) / 2,
                Low = (q.High + q.Low) / 2,
                Close = (q.High + q.Low) / 2,
                Volume = q.Volume
            }).ToList();
        })).Value;
    }

    public IReadOnlyList<CandleMt5> ObterHistoricoCandles() => _candles;
    public TickEvent ObterTick() => _tick;
    public EstrategiaConfig ObterConfig() => _config;

    private double? ObterValorBase(Operando op)
    {
        if (op.Fonte == TipoFonte.Literal) return op.ValorLiteral;

        if (op.Fonte is >= TipoFonte.CandleAbertura and <= TipoFonte.CandlePosFechamentoRange)
        {
            var idx = op.Indice ?? 0;
            
            if (!string.IsNullOrEmpty(op.Timeframe))
            {
                var tfQuotes = ObterQuotesParaTimeframe(op.Timeframe);
                if (idx >= tfQuotes.Count) return null;
                var q = tfQuotes[tfQuotes.Count - 1 - idx];
                
                return op.Fonte switch
                {
                    TipoFonte.CandleAbertura => (double)q.Open,
                    TipoFonte.CandleMaximo => (double)q.High,
                    TipoFonte.CandleMinimo => (double)q.Low,
                    TipoFonte.CandleFechamento => (double)q.Close,
                    TipoFonte.CandleVolume => (double)q.Volume,
                    TipoFonte.CandleRange => (double)(q.High - q.Low),
                    TipoFonte.CandleCorpo => (double)(q.Close - q.Open),
                    TipoFonte.CandleCorpoAbs => (double)Math.Abs(q.Close - q.Open),
                    TipoFonte.CandlePavioSuperior => (double)(Math.Max(q.Open, q.Close) < q.High ? q.High - Math.Max(q.Open, q.Close) : 0),
                    TipoFonte.CandlePavioInferior => (double)(Math.Min(q.Open, q.Close) > q.Low ? Math.Min(q.Open, q.Close) - q.Low : 0),
                    TipoFonte.CandlePosFechamentoRange => (double)(q.High - q.Low > 0 ? (q.Close - q.Low) / (q.High - q.Low) : 0),
                    _ => null
                };
            }
            else
            {
                if (idx >= _candles.Count) return null;
                var c = _candles[_candles.Count - 1 - idx]; 

                return op.Fonte switch
                {
                    TipoFonte.CandleAbertura => c.Abertura,
                    TipoFonte.CandleMaximo => c.Maximo,
                    TipoFonte.CandleMinimo => c.Minimo,
                    TipoFonte.CandleFechamento => c.Fechamento,
                    TipoFonte.CandleVolume => c.Volume,
                    TipoFonte.CandleRange => c.Maximo - c.Minimo,
                    TipoFonte.CandleCorpo => c.Fechamento - c.Abertura,
                    TipoFonte.CandleCorpoAbs => Math.Abs(c.Fechamento - c.Abertura),
                    TipoFonte.CandlePavioSuperior => Math.Max(c.Abertura, c.Fechamento) < c.Maximo ? c.Maximo - Math.Max(c.Abertura, c.Fechamento) : 0,
                    TipoFonte.CandlePavioInferior => Math.Min(c.Abertura, c.Fechamento) > c.Minimo ? Math.Min(c.Abertura, c.Fechamento) - c.Minimo : 0,
                    TipoFonte.CandlePosFechamentoRange => c.Maximo - c.Minimo > 0 ? (c.Fechamento - c.Minimo) / (c.Maximo - c.Minimo) : 0,
                    _ => null
                };
            }
        }

        if (op.Fonte is TipoFonte.TickBid or TipoFonte.TickAsk or TipoFonte.TickSpread)
        {
            return op.Fonte switch
            {
                TipoFonte.TickBid => _tick.Bid,
                TipoFonte.TickAsk => _tick.Ask,
                TipoFonte.TickSpread => Math.Abs(_tick.Ask - _tick.Bid), 
                _ => null
            };
        }

        if (op.Fonte is TipoFonte.PosicaoPrecoEntrada or TipoFonte.PosicaoLucroBruto or TipoFonte.PosicaoLucroPercentualPreco)
        {
            if (PosicaoAtiva is null) throw new InvalidOperationException($"Operando '{op.Fonte}' requisitado, mas nenhuma posição ativa está no contexto.");
            return op.Fonte switch
            {
                TipoFonte.PosicaoPrecoEntrada => PosicaoAtiva.PrecoAbertura,
                TipoFonte.PosicaoLucroBruto => PosicaoAtiva.LucroBruto,
                TipoFonte.PosicaoLucroPercentualPreco => PosicaoAtiva.Compra 
                    ? ((PosicaoAtiva.PrecoAtual - PosicaoAtiva.PrecoAbertura) / PosicaoAtiva.PrecoAbertura) * 100.0
                    : ((PosicaoAtiva.PrecoAbertura - PosicaoAtiva.PrecoAtual) / PosicaoAtiva.PrecoAbertura) * 100.0,
                _ => null
            };
        }

        if (op.Fonte is TipoFonte.NivelSuportePreco or TipoFonte.NivelResistenciaPreco)
        {
            return EncontrarNivelProximo(op.Fonte == TipoFonte.NivelSuportePreco ? "suporte" : "resistencia");
        }

        if (op.Fonte == TipoFonte.Ref)
        {
            if (string.IsNullOrEmpty(op.ParametroString)) return null;
            var parts = op.ParametroString.Split('.');
            if (parts.Length != 2) return null;
            var nome = parts[0];
            var prop = parts[1];

            var cRef = ObterRef(nome);
            if (cRef == null) return null;

            return prop switch
            {
                "abertura" => cRef.Abertura,
                "maximo" => cRef.Maximo,
                "minimo" => cRef.Minimo,
                "fechamento" => cRef.Fechamento,
                "volume" => cRef.Volume,
                "range" => cRef.Maximo - cRef.Minimo,
                "corpo" => cRef.Fechamento - cRef.Abertura,
                "corpoAbs" => Math.Abs(cRef.Fechamento - cRef.Abertura),
                "pavioSuperior" => Math.Max(cRef.Abertura, cRef.Fechamento) < cRef.Maximo ? cRef.Maximo - Math.Max(cRef.Abertura, cRef.Fechamento) : 0,
                "pavioInferior" => Math.Min(cRef.Abertura, cRef.Fechamento) > cRef.Minimo ? Math.Min(cRef.Abertura, cRef.Fechamento) - cRef.Minimo : 0,
                _ => null
            };
        }

        if (op.Fonte is TipoFonte.Maxima or TipoFonte.Minima)
        {
            var from = op.Periodo ?? 0;
            var to = op.Periodo2 ?? 0;
            if (from > to) (from, to) = (to, from);
            var lastIdx = _candles.Count - 1;
            if (lastIdx - to < 0) return null;

            double? best = null;
            for (int i = from; i <= to; i++)
            {
                var c = _candles[lastIdx - i];
                var val = op.Fonte == TipoFonte.Maxima ? c.Maximo : c.Minimo;
                if (best == null) best = val;
                else if (op.Fonte == TipoFonte.Maxima && val > best) best = val;
                else if (op.Fonte == TipoFonte.Minima && val < best) best = val;
            }
            return best;
        }

        if (op.Fonte is TipoFonte.MaximaJanela or TipoFonte.MinimaJanela)
        {
            if (string.IsNullOrEmpty(op.ParametroString)) return null;
            var parts = op.ParametroString.Split(',');
            if (parts.Length != 2) return null;
            if (!TimeSpan.TryParse(parts[0], out var inicio) || !TimeSpan.TryParse(parts[1], out var fim)) return null;

            var currDate = _tick.Timestamp.Date;
            double? best = null;

            for (int i = 0; i < _candles.Count; i++)
            {
                var c = _candles[i];
                if (c.Tempo.Date != currDate) continue;
                
                var t = c.Tempo.TimeOfDay;
                bool inWindow = (inicio <= fim) ? (t >= inicio && t <= fim) : (t >= inicio || t <= fim);

                if (inWindow)
                {
                    var val = (op.Fonte == TipoFonte.MaximaJanela) ? c.Maximo : c.Minimo;
                    if (best == null || (op.Fonte == TipoFonte.MaximaJanela ? val > best : val < best)) best = val;
                }
            }
            return best;
        }

        if (op.Fonte == TipoFonte.RangeMedio)
        {
            var lookback = op.Periodo ?? 20;
            if (_candles.Count < lookback) return null;
            double sum = 0;
            for(int i = 0; i < lookback; i++)
            {
                var c = _candles[_candles.Count - 1 - i];
                sum += (c.Maximo - c.Minimo);
            }
            return sum / lookback;
        }

        if (op.Fonte == TipoFonte.FibonacciRetracao)
        {
            return CalcularFibonacciRetracao(op);
        }

        return ObterValorIndicador(op);
    }

    private double? CalcularFibonacciRetracao(Operando op)
    {
        var from = op.Periodo ?? 0;
        var to = op.Periodo2 ?? 0;
        if (from > to) (from, to) = (to, from);
        if (from < 0 || to < 0) return null;

        var lastIdx = _candles.Count - 1;
        if (lastIdx - to < 0) return null;

        double? maximo = null;
        double? minimo = null;
        for (var i = from; i <= to; i++)
        {
            var candle = _candles[lastIdx - i];
            maximo = maximo is null ? candle.Maximo : Math.Max(maximo.Value, candle.Maximo);
            minimo = minimo is null ? candle.Minimo : Math.Min(minimo.Value, candle.Minimo);
        }

        if (maximo is null || minimo is null) return null;

        var range = maximo.Value - minimo.Value;
        if (range <= 0) return null;

        var nivel = op.NivelFibonacci ?? 0.618;
        return op.DirecaoFibonacci?.Equals("baixa", StringComparison.OrdinalIgnoreCase) == true
            ? minimo.Value + (range * nivel)
            : maximo.Value - (range * nivel);
    }

    private double? ObterValorIndicador(Operando op)
    {
        var p = op.Periodo ?? 14;
        var idx = op.Indice ?? 0;
        var key = $"{op.Fonte}_{op.ParametroString}_{p}_{op.Timeframe}";

        var array = _cacheIndicadores.GetOrAdd(key, k => CalcularIndicador(op.Fonte, p, op.ParametroString, op.Timeframe));
        if (array == null || idx >= array.Length) return null;
        
        return array[array.Length - 1 - idx];
    }

    private double?[] CalcularIndicador(TipoFonte fonte, int periodo, string? paramStr, string? timeframe)
    {
        var quotes = ObterQuotesParaTimeframe(timeframe);
        if (quotes.Count < periodo) return [];

        return fonte switch
        {
            TipoFonte.Ema => quotes.GetEma(periodo).Select(x => (double?)x.Ema).ToArray(),
            TipoFonte.Sma => quotes.GetSma(periodo).Select(x => (double?)x.Sma).ToArray(),
            TipoFonte.Smma => quotes.GetSmma(periodo).Select(x => (double?)x.Smma).ToArray(),
            TipoFonte.Rsi => quotes.GetRsi(periodo).Select(x => (double?)x.Rsi).ToArray(),
            TipoFonte.Atr => quotes.GetAtr(periodo).Select(x => (double?)x.Atr).ToArray(),
            TipoFonte.Vwap => quotes.GetVwap().Select(x => (double?)x.Vwap).ToArray(), 
            TipoFonte.MediaPrecoMediano => paramStr != null && paramStr.Trim('"', '\'') == "SMMA" 
                ? ObterQuotesMedianParaTimeframe(timeframe).GetSmma(periodo).Select(x => (double?)x.Smma).ToArray()
                : ObterQuotesMedianParaTimeframe(timeframe).GetSma(periodo).Select(x => (double?)x.Sma).ToArray(),
            _ => []
        };
    }

    private double? EncontrarNivelProximo(string tipoEsperado)
    {
        var niveis = Indicators.ParametroParser.ObterNiveis(_config.Parametros, "niveis");
        var precoBase = _tick.Bid;

        var filtrados = niveis.Where(n => n.Tipo.Equals(tipoEsperado, StringComparison.OrdinalIgnoreCase)).ToList();
        if (filtrados.Count == 0) return null;

        return filtrados.OrderBy(n => Math.Abs(n.Preco - precoBase)).First().Preco;
    }
}
