using Financial.Robot.Domain.Events;
using Financial.Robot.Domain.ValueObjects;
using Financial.Robot.Domain.Interfaces;
using Financial.Robot.Worker.Indicators;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public sealed class EstrategiaInterpretada : IEstrategiaEntrada, IEstrategiaSaida
{
    public string Nome => "EstrategiaInterpretada";

    public static Microsoft.Extensions.Logging.ILogger? Logger { get; set; }

    private record CacheEntry(DefinicaoEstrategia? Definicao, IReadOnlyList<string>? Erros, int Lookback, bool FoiRecarregado = false);
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, CacheEntry> _cache = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, System.Timers.Timer> _timers = new();

    public int ObterLookbackNecessario(EstrategiaConfig config)
    {
        var entrada = ObterOuCarregarCache(config);
        return entrada.Lookback;
    }

    public ResultadoDecisao Avaliar(
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var entrada = ObterOuCarregarCache(config);

        if (entrada.Erros != null && entrada.Erros.Count > 0)
        {
            return ResultadoDecisao.Aguardar($"Definição inválida: {string.Join("; ", entrada.Erros)}");
        }

        if (entrada.Definicao == null)
            return ResultadoDecisao.Aguardar("Definição não carregada.");

        if (candles.Count < entrada.Lookback)
        {
            if (entrada.FoiRecarregado)
                return ResultadoDecisao.Aguardar("Histórico insuficiente após recarga da definição.");
            
            return ResultadoDecisao.Aguardar($"Aguardando histórico: {candles.Count}/{entrada.Lookback}");
        }

        var ctx = new ContextoAvaliacao(candles, tick, config);

        if (entrada.Definicao.Filtros != null)
        {
            foreach (var f in entrada.Definicao.Filtros)
            {
                var (passou, motivoFiltro) = AvaliarFiltro(f, ctx);
                if (!passou) return ResultadoDecisao.Aguardar($"filtros: {motivoFiltro}");
            }
        }

        if (config.Comprar && entrada.Definicao.Compra != null)
        {
            bool passouSetup = true;
            string rastroSetup = "setup omitido";
            if (entrada.Definicao.Compra.Setup != null)
            {
                ctx.LimparRefs();
                var (p, r) = AvaliadorCondicoes.AvaliarCondicao(entrada.Definicao.Compra.Setup, ctx);
                passouSetup = p;
                rastroSetup = r;
            }

            if (passouSetup)
            {
                bool passouGatilho = true;
                string rastroGatilho = "gatilho omitido";
                if (entrada.Definicao.Compra.Gatilho != null)
                {
                    var (p, r) = AvaliadorCondicoes.AvaliarCondicao(entrada.Definicao.Compra.Gatilho, ctx);
                    passouGatilho = p;
                    rastroGatilho = r;
                }

                if (passouGatilho)
                {
                    var stop = CalculadoraStop.Calcular(entrada.Definicao.Compra.Stop, ctx, true);
                    return ResultadoDecisao.Comprar($"compra/setup: {rastroSetup} | gatilho: {rastroGatilho}", stop);
                }
            }
        }

        if (config.Vender && entrada.Definicao.Venda != null)
        {
            bool passouSetup = true;
            string rastroSetup = "setup omitido";
            if (entrada.Definicao.Venda.Setup != null)
            {
                ctx.LimparRefs();
                var (p, r) = AvaliadorCondicoes.AvaliarCondicao(entrada.Definicao.Venda.Setup, ctx);
                passouSetup = p;
                rastroSetup = r;
            }

            if (passouSetup)
            {
                bool passouGatilho = true;
                string rastroGatilho = "gatilho omitido";
                if (entrada.Definicao.Venda.Gatilho != null)
                {
                    var (p, r) = AvaliadorCondicoes.AvaliarCondicao(entrada.Definicao.Venda.Gatilho, ctx);
                    passouGatilho = p;
                    rastroGatilho = r;
                }

                if (passouGatilho)
                {
                    var stop = CalculadoraStop.Calcular(entrada.Definicao.Venda.Stop, ctx, false);
                    return ResultadoDecisao.Vender($"venda/setup: {rastroSetup} | gatilho: {rastroGatilho}", stop);
                }
            }
        }

        return ResultadoDecisao.Aguardar("Aguardando condições de entrada.");
    }

    public (bool Fechar, string Motivo) AvaliarSaida(
        bool isCompra,
        IReadOnlyList<CandleMt5> candles,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadores,
        IReadOnlyList<(IndicadorConfig Config, ResultadoIndicador Resultado)> indicadoresMultiFonte,
        DetalhesPosicaoMt5 posicao,
        TickEvent tick,
        EstrategiaConfig config)
    {
        var entrada = ObterOuCarregarCache(config);
        if (entrada.Definicao?.Saida == null) return (false, string.Empty);

        var bloco = isCompra ? entrada.Definicao.Saida.Compra : entrada.Definicao.Saida.Venda;
        if (bloco == null) return (false, string.Empty);

        var ctx = new ContextoAvaliacao(candles, tick, config, posicao);
        var (passou, motivo) = AvaliadorCondicoes.AvaliarCondicao(bloco, ctx);

        if (passou)
        {
            var ladoStr = isCompra ? "compra" : "venda";
            var motivoLog = $"saida/{ladoStr}: {motivo}";
            Logger?.LogInformation("Saida técnica ({Lado}) disparada: {Motivo}", ladoStr, motivo);
            return (true, motivoLog);
        }

        return (false, string.Empty);
    }

    private CacheEntry ObterOuCarregarCache(EstrategiaConfig config)
    {
        var caminho = ParametroParser.ObterString(config.Parametros, "arquivoDefinicao", "");
        
        if (string.IsNullOrWhiteSpace(caminho))
        {
            return new CacheEntry(null, ["'arquivoDefinicao' não configurado nos parâmetros."], 0);
        }

        return _cache.GetOrAdd(caminho, key =>
        {
            var (def, erros) = CarregadorDefinicao.Carregar(key);

            var fullPath = CarregadorDefinicao.ObterCaminhoCompleto(key);
            if (fullPath != null && !_watchers.ContainsKey(key))
            {
                var dir = Path.GetDirectoryName(fullPath);
                var file = Path.GetFileName(fullPath);
                if (dir != null && file != null)
                {
                    var watcher = new FileSystemWatcher(dir, file)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                        EnableRaisingEvents = true
                    };
                    watcher.Changed += (s, e) => DispararRecarga(key);
                    watcher.Created += (s, e) => DispararRecarga(key);
                    _watchers.TryAdd(key, watcher);
                }
            }

            if (erros.Count > 0)
            {
                return new CacheEntry(null, erros, 0);
            }
            else
            {
                return new CacheEntry(def, null, DerivarLookback(def!));
            }
        });
    }

    private static void DispararRecarga(string key)
    {
        var timer = _timers.GetOrAdd(key, k => 
        {
            var t = new System.Timers.Timer(500) { AutoReset = false };
            t.Elapsed += (s, e) => RecarregarArquivo(k);
            return t;
        });
        
        timer.Stop();
        timer.Start();
    }

    private static void RecarregarArquivo(string key)
    {
        var (def, erros) = CarregadorDefinicao.Carregar(key);
        if (erros.Count == 0 && def != null)
        {
            _cache[key] = new CacheEntry(def, null, DerivarLookback(def), true);
            var msg = $"[EstrategiaInterpretada] Hot-reload aplicado com sucesso para {key}";
            if (Logger != null) Logger.LogInformation(msg);
            else Console.WriteLine(msg);
        }
        else
        {
            var msg = $"[EstrategiaInterpretada] Erro no hot-reload de {key}: {string.Join("; ", erros)}";
            if (Logger != null) Logger.LogError(msg);
            else Console.WriteLine(msg);
        }
    }

    private static int DerivarLookback(DefinicaoEstrategia def)
    {
        var operandosText = new List<string>();
        int lookbackExtra = 0;
        int max = 20; 
        Action<CondicaoDef>? traverse = null;
        traverse = c =>
        {
            if (c.Op is "cruzouAcima" or "cruzouAbaixo") lookbackExtra = Math.Max(lookbackExtra, 1);
            if (c.Op is "inclinacao" && c.Candles.HasValue) lookbackExtra = Math.Max(lookbackExtra, c.Candles.Value);

            if (c.Candle.HasValue && c.Candle.Value > max) max = c.Candle.Value;
            if (c.FimCandleSinal.HasValue) 
            {
                var req = c.FimCandleSinal.Value + (c.MaxCandlesImpulso ?? 6) + (c.MaxCandlesPullback ?? 5);
                if (req > max) max = req;
            }

            if (c.A != null) operandosText.Add(c.A);
            if (c.B != null) operandosText.Add(c.B);
            if (c.Min != null) operandosText.Add(c.Min);
            if (c.Max != null) operandosText.Add(c.Max);
            if (c.Serie != null) operandosText.AddRange(c.Serie);
            if (c.Todas != null) foreach(var child in c.Todas) traverse?.Invoke(child);
            if (c.Qualquer != null) foreach(var child in c.Qualquer) traverse?.Invoke(child);
        };

        if (def.Compra != null)
        {
            if (def.Compra.Setup != null) traverse(def.Compra.Setup);
            if (def.Compra.Gatilho != null) traverse(def.Compra.Gatilho);
            if (def.Compra.Stop?.A != null) operandosText.Add(def.Compra.Stop.A);
            if (def.Compra.Stop?.Candle.HasValue == true && def.Compra.Stop.Candle.Value > max) max = def.Compra.Stop.Candle.Value;
        }
        if (def.Venda != null)
        {
            if (def.Venda.Setup != null) traverse(def.Venda.Setup);
            if (def.Venda.Gatilho != null) traverse(def.Venda.Gatilho);
            if (def.Venda.Stop?.A != null) operandosText.Add(def.Venda.Stop.A);
            if (def.Venda.Stop?.Candle.HasValue == true && def.Venda.Stop.Candle.Value > max) max = def.Venda.Stop.Candle.Value;
        }
        if (def.Saida != null)
        {
            if (def.Saida.Compra != null) traverse(def.Saida.Compra);
            if (def.Saida.Venda != null) traverse(def.Saida.Venda);
        }

        if (def.Filtros != null)
        {
            foreach (var f in def.Filtros)
            {
                if (f.Periodo.HasValue && f.Periodo.Value > max) max = f.Periodo.Value;
                if (f.LookbackMedioRange.HasValue && f.LookbackMedioRange.Value > max) max = f.LookbackMedioRange.Value;
                if (f.JanelaCandles.HasValue && f.JanelaCandles.Value > max) max = f.JanelaCandles.Value;
            }
        }

        foreach (var text in operandosText)
        {
            if (ParserOperando.TryParse(text, out var op, out _))
            {
                var req = (op.Indice ?? 0) + (op.Periodo ?? 0);
                var req2 = (op.Indice ?? 0) + (op.Periodo2 ?? 0);
                if (req > max) max = req;
                if (req2 > max) max = req2;
            }
        }

        return max + lookbackExtra + 5;
    }

    private (bool Passou, string Rastro) AvaliarFiltro(FiltroDef f, ContextoAvaliacao ctx)
    {
        switch (f.Tipo)
        {
            case "spreadMaximo":
                {
                    var spread = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.TickSpread });
                    var pass = spread <= f.ValorPreco;
                    return (pass, pass ? $"{f.Tipo} OK ({spread:F1}<={f.ValorPreco})" : $"{f.Tipo} FALHOU ({spread:F1}>{f.ValorPreco})");
                }
            case "janelaHorario":
                {
                    var agora = ctx.ObterTick().Timestamp.TimeOfDay;
                    var inicio = TimeSpan.Parse(f.Inicio!);
                    var fim = TimeSpan.Parse(f.Fim!);
                    bool noHorario;
                    if (inicio <= fim) noHorario = agora >= inicio && agora <= fim;
                    else noHorario = agora >= inicio || agora <= fim;
                    return (noHorario, noHorario ? $"{f.Tipo} OK" : $"{f.Tipo} FALHOU (fora da janela)");
                }
            case "atrMinimo":
                {
                    var atrMin = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.Atr, Periodo = f.Periodo });
                    var passAtrMin = atrMin >= f.ValorPreco;
                    return (passAtrMin, passAtrMin ? $"{f.Tipo} OK ({atrMin:F1}>={f.ValorPreco})" : $"{f.Tipo} FALHOU ({atrMin:F1}<{f.ValorPreco})");
                }
            case "atrMaximo":
                {
                    var atrMax = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.Atr, Periodo = f.Periodo });
                    var passAtrMax = atrMax <= f.ValorPreco;
                    return (passAtrMax, passAtrMax ? $"{f.Tipo} OK ({atrMax:F1}<={f.ValorPreco})" : $"{f.Tipo} FALHOU ({atrMax:F1}>{f.ValorPreco})");
                }
            case "candleMaxAtrMultiplo":
                {
                    var atrMulti = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.Atr, Periodo = f.Periodo });
                    var range = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.CandleRange, Indice = 0 });
                    var passMaxMulti = range <= atrMulti * f.Multiplo;
                    return (passMaxMulti, passMaxMulti ? $"{f.Tipo} OK" : $"{f.Tipo} FALHOU (range excessivo)");
                }
            case "semClimax":
                {
                    var rangeMed = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.RangeMedio, Periodo = f.LookbackMedioRange });
                    if (rangeMed is null) return (false, $"{f.Tipo} FALHOU (rangeMed nulo)");
                    var limite = rangeMed * f.MultiploRange;
                    for (int i = 0; i < f.JanelaCandles; i++)
                    {
                        var r = ctx.ResolverOperando(new Operando { Fonte = TipoFonte.CandleRange, Indice = i });
                        if (r > limite) return (false, $"{f.Tipo} FALHOU (climax no candle {i})");
                    }
                    return (true, $"{f.Tipo} OK");
                }
            default:
                return (false, $"{f.Tipo} não implementado");
        }
    }
}
