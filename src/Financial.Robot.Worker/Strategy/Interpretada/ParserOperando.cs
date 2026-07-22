using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public static class ParserOperando
{
    private static readonly Regex OperandoRegex = new(
        @"^(?<fonte>.+?)(?:\s*(?<mod>[\+\-\*])\s*(?<val>\d+(?:\.\d+)?))?$", 
        RegexOptions.Compiled);

    public static bool TryParse(string texto, [NotNullWhen(true)] out Operando? operando, [NotNullWhen(false)] out string? erro)
    {
        operando = null;
        erro = null;

        if (string.IsNullOrWhiteSpace(texto))
        {
            erro = "Operando vazio.";
            return false;
        }

        if (double.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out var lit))
        {
            operando = new Operando { Fonte = TipoFonte.Literal, ValorLiteral = lit };
            return true;
        }

        var matchOp = OperandoRegex.Match(texto);
        if (!matchOp.Success)
        {
            erro = $"Sintaxe inválida para operando: {texto}";
            return false;
        }

        var fonteText = matchOp.Groups["fonte"].Value.Trim();
        char? mod = null;
        double? modVal = null;

        if (matchOp.Groups["mod"].Success)
        {
            mod = matchOp.Groups["mod"].Value[0];
            modVal = double.Parse(matchOp.Groups["val"].Value, CultureInfo.InvariantCulture);
        }

        if (!TryParseFonte(fonteText, out var fonte, out var paramStr, out var p1, out var p2, out var timeframe, out var indice, out var subProp, out var nivelFibonacci, out var direcaoFibonacci, out erro))
        {
            return false;
        }
        
        if (!MapearTipoFonte(fonte, subProp, out var tipoFonte, out var mapeamentoErro))
        {
            erro = mapeamentoErro;
            return false;
        }

        if (indice < 0)
        {
            erro = $"Índice não pode ser negativo: {texto}";
            return false;
        }

        if (tipoFonte == TipoFonte.Ref)
        {
            paramStr = subProp;
        }

        operando = new Operando
        {
            Fonte = tipoFonte,
            ParametroString = paramStr,
            Periodo = p1,
            Periodo2 = p2,
            Timeframe = timeframe,
            Indice = indice,
            Modificador = mod,
            ValorModificador = modVal,
            NivelFibonacci = nivelFibonacci,
            DirecaoFibonacci = direcaoFibonacci
        };

        return true;
    }

    private static bool TryParseFonte(
        string texto,
        out string fonteBase,
        out string? paramStr,
        out int? p1,
        out int? p2,
        out string? timeframe,
        out int? indice,
        out string? subProp,
        out double? nivelFibonacci,
        out string? direcaoFibonacci,
        [NotNullWhen(false)] out string? erro)
    {
        fonteBase = "";
        paramStr = null;
        p1 = null;
        p2 = null;
        timeframe = null;
        indice = null;
        subProp = null;
        nivelFibonacci = null;
        direcaoFibonacci = null;
        erro = null;

        var m = Regex.Match(texto, @"^(?<base>[a-zA-Z]+)(?:\((?<args>[^\)]+)\))?(?:\[(?<ind>\-?\d+)\])?(?:\.(?<prop>[a-zA-Z\.]+))?$");
        if (!m.Success)
        {
            erro = $"Fonte não reconhecida na gramática: {texto}";
            return false;
        }

        fonteBase = m.Groups["base"].Value;
        
        if (m.Groups["args"].Success)
        {
            var argsStr = m.Groups["args"].Value;
            var parts = argsStr.Split(',', StringSplitOptions.TrimEntries);
            if (fonteBase.Equals("fibRet", StringComparison.OrdinalIgnoreCase))
            {
                if (parts.Length != 4)
                {
                    erro = "fibRet requer 4 argumentos: fibRet(de, ate, nivel, \"alta|baixa\").";
                    return false;
                }

                if (!int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var de)
                    || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ate))
                {
                    erro = "fibRet requer indices inteiros nos dois primeiros argumentos.";
                    return false;
                }

                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var nivel)
                    || nivel <= 0
                    || nivel >= 1)
                {
                    erro = "fibRet requer nivel entre 0 e 1, por exemplo 0.618.";
                    return false;
                }

                var direcao = parts[3].Trim('"', '\'').ToLowerInvariant();
                if (direcao is not ("alta" or "baixa"))
                {
                    erro = "fibRet requer direcao \"alta\" ou \"baixa\".";
                    return false;
                }

                p1 = de;
                p2 = ate;
                nivelFibonacci = nivel;
                direcaoFibonacci = direcao;
                return true;
            }

            if (parts.Length == 1)
            {
                if (int.TryParse(parts[0], out var val)) 
                {
                    p1 = val;
                }
                else 
                {
                    var p = parts[0].Trim('"');
                    if (Regex.IsMatch(p, @"^(M[1-9][0-9]*|H[1-9][0-9]*|D[1-9][0-9]*|W[1-9][0-9]*|MN)$", RegexOptions.IgnoreCase))
                    {
                        timeframe = p.ToUpper();
                    }
                    else 
                    {
                        paramStr = p;
                    }
                }
            }
            else if (parts.Length >= 2)
            {
                if (int.TryParse(parts[0], out var val1) && int.TryParse(parts[1], out var val2))
                {
                    p1 = val1;
                    p2 = val2;
                }
                else if (!int.TryParse(parts[0], out _) && int.TryParse(parts[1], out var val2_2))
                {
                    paramStr = parts[0].Trim('"');
                    p1 = val2_2;
                }
                else if (int.TryParse(parts[0], out var val1_2) && !int.TryParse(parts[1], out _))
                {
                    p1 = val1_2;
                    var tStr = parts[1].Trim('"');
                    if (Regex.IsMatch(tStr, @"^(M[1-9][0-9]*|H[1-9][0-9]*|D[1-9][0-9]*|W[1-9][0-9]*|MN)$", RegexOptions.IgnoreCase))
                    {
                        timeframe = tStr.ToUpper();
                    }
                    else
                    {
                        erro = $"Timeframe inválido ou segundo argumento não suportado: {tStr}";
                        return false;
                    }
                }
                else
                {
                    paramStr = string.Join(",", parts.Select(x => x.Trim('"')));
                }
            }
        }

        if (m.Groups["ind"].Success)
        {
            indice = int.Parse(m.Groups["ind"].Value);
        }

        if (m.Groups["prop"].Success)
        {
            subProp = m.Groups["prop"].Value;
        }

        return true;
    }

    private static bool MapearTipoFonte(string fonte, string? prop, out TipoFonte tipo, [NotNullWhen(false)] out string? erro)
    {
        tipo = TipoFonte.Literal;
        erro = null;
        
        if (fonte == "candle")
        {
            tipo = prop switch
            {
                "abertura" => TipoFonte.CandleAbertura,
                "maximo" => TipoFonte.CandleMaximo,
                "minimo" => TipoFonte.CandleMinimo,
                "fechamento" => TipoFonte.CandleFechamento,
                "volume" => TipoFonte.CandleVolume,
                "range" => TipoFonte.CandleRange,
                "corpo" => TipoFonte.CandleCorpo,
                "corpoAbs" => TipoFonte.CandleCorpoAbs,
                "pavioSuperior" => TipoFonte.CandlePavioSuperior,
                "pavioInferior" => TipoFonte.CandlePavioInferior,
                "posFechamentoRange" => TipoFonte.CandlePosFechamentoRange,
                _ => TipoFonte.Literal
            };
            if (tipo == TipoFonte.Literal) { erro = $"Propriedade desconhecida para candle: {prop}"; return false; }
            return true;
        }
        
        if (fonte == "tick")
        {
            tipo = prop switch
            {
                "bid" => TipoFonte.TickBid,
                "ask" => TipoFonte.TickAsk,
                "spread" => TipoFonte.TickSpread,
                _ => TipoFonte.Literal
            };
            if (tipo == TipoFonte.Literal) { erro = $"Propriedade desconhecida para tick: {prop}"; return false; }
            return true;
        }
        
        if (fonte == "nivel")
        {
            if (prop == "suporte.preco") { tipo = TipoFonte.NivelSuportePreco; return true; }
            if (prop == "resistencia.preco") { tipo = TipoFonte.NivelResistenciaPreco; return true; }
            erro = $"Propriedade desconhecida para nivel: {prop}";
            return false;
        }
        
        if (fonte == "ref")
        {
            if (string.IsNullOrEmpty(prop)) { erro = "Ref precisa especificar nome.propriedade (ex: ref.sinal.maximo)"; return false; }
            tipo = TipoFonte.Ref;
            return true;
        }

        if (fonte == "posicao")
        {
            tipo = prop switch
            {
                "precoEntrada" => TipoFonte.PosicaoPrecoEntrada,
                "lucroBruto" => TipoFonte.PosicaoLucroBruto,
                "lucroPercentualPreco" => TipoFonte.PosicaoLucroPercentualPreco,
                _ => TipoFonte.Literal
            };
            if (tipo == TipoFonte.Literal) { erro = $"Propriedade desconhecida para posicao: {prop}"; return false; }
            return true;
        }

        tipo = fonte switch
        {
            "ema" => TipoFonte.Ema,
            "sma" => TipoFonte.Sma,
            "smma" => TipoFonte.Smma,
            "rsi" => TipoFonte.Rsi,
            "atr" => TipoFonte.Atr,
            "vwap" => TipoFonte.Vwap,
            "mediaPrecoMediano" => TipoFonte.MediaPrecoMediano,
            "maxima" => TipoFonte.Maxima,
            "minima" => TipoFonte.Minima,
            "maximaJanela" => TipoFonte.MaximaJanela,
            "minimaJanela" => TipoFonte.MinimaJanela,
            "rangeMedio" => TipoFonte.RangeMedio,
            "fibRet" => TipoFonte.FibonacciRetracao,
            _ => TipoFonte.Literal
        };
        
        if (tipo == TipoFonte.Literal)
        {
            erro = $"Fonte base desconhecida: {fonte}";
            return false;
        }
        
        return true;
    }
}
