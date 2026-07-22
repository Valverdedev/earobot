namespace Financial.Robot.Worker.Strategy.Interpretada;

public static class AvaliadorCondicoes
{
    public static (bool Passou, string Rastro) AvaliarGrupo(IReadOnlyList<CondicaoDef>? condicoes, ContextoAvaliacao ctx, bool isQualquer)
    {
        if (condicoes is null || condicoes.Count == 0) return (true, "");

        var rastros = new List<string>();
        bool finalResult = !isQualquer;

        foreach (var c in condicoes)
        {
            var (passou, rastro) = AvaliarCondicao(c, ctx);
            rastros.Add(rastro);

            if (isQualquer)
            {
                if (passou) finalResult = true;
            }
            else
            {
                if (!passou) finalResult = false;
            }
        }

        var opStr = isQualquer ? " | " : " | ";
        var rastroStr = string.Join(opStr, rastros);
        return (finalResult, rastroStr);
    }

    public static (bool Passou, string Rastro) AvaliarCondicao(CondicaoDef def, ContextoAvaliacao ctx)
    {
        if (def.Todas is not null)
        {
            var (passou, r) = AvaliarGrupo(def.Todas, ctx, false);
            return (passou, r);
        }

        if (def.Qualquer is not null)
        {
            var (passou, r) = AvaliarGrupo(def.Qualquer, ctx, true);
            return (passou, $"({r})");
        }

        if (def.Padrao is not null)
        {
            if (def.Padrao is "impulsoPullback" or "rompimentoReteste" or "tocouNivel")
            {
                var passou = PadroesCompostos.Avaliar(def, ctx, out var rastro);
                return (passou, rastro);
            }
            else
            {
                var passou = PadroesCandle.Avaliar(def, ctx, out var rastro);
                return (passou, rastro);
            }
        }

        return AvaliarOperador(def, ctx);
    }

    private static (bool Passou, string Rastro) AvaliarOperador(CondicaoDef def, ContextoAvaliacao ctx)
    {
        bool result = false;
        string detalhe = "";

        if (def.Op is "alinhados")
        {
            if (def.Ordem != "crescente" && def.Ordem != "decrescente")
                return (false, $"{def.Op} FALHOU (ordem inválida ou não especificada)");

            var vals = new List<double>();
            foreach (var s in def.Serie ?? [])
            {
                if (ParserOperando.TryParse(s, out var op, out var erroParse))
                {
                    var v = ctx.ResolverOperando(op);
                    if (v is null) return (false, $"{def.Op} FALHOU (valor nulo em {s})");
                    vals.Add(v.Value);
                }
                else
                {
                    return (false, $"{def.Op} FALHOU (erro de parse em {s}: {erroParse})");
                }
            }

            result = true;
            for (int i = 0; i < vals.Count - 1; i++)
            {
                if (def.Ordem == "crescente" && vals[i] >= vals[i + 1]) result = false;
                if (def.Ordem == "decrescente" && vals[i] <= vals[i + 1]) result = false;
            }
            
            detalhe = $"[{string.Join(", ", vals.Select(v => v.ToString("F2")))}]";
            return (result, result ? $"{def.Op} OK {detalhe}".TrimEnd() : $"{def.Op} FALHOU {detalhe}".TrimEnd());
        }

        if (def.Op is "inclinacao")
        {
            if (ParserOperando.TryParse(def.A ?? "", out var opA, out var errA))
            {
                var v0 = ctx.ResolverOperando(opA);
                var opN = opA with { Indice = (opA.Indice ?? 0) + (def.Candles ?? 1) };
                var vN = ctx.ResolverOperando(opN);

                if (v0 is null || vN is null) return (false, $"{def.Op} FALHOU (valor nulo)");

                if (def.Direcao == "alta") result = v0 > vN;
                else if (def.Direcao == "baixa") result = v0 < vN;
                else return (false, $"{def.Op} FALHOU (direção inválida ou nula)");

                detalhe = $"(atual={v0:F2}, ant={vN:F2})";
                return (result, result ? $"{def.Op} OK {detalhe}".TrimEnd() : $"{def.Op} FALHOU {detalhe}".TrimEnd());
            }
            return (false, $"{def.Op} FALHOU (parse 'a': {errA})");
        }

        if (def.Op is "entre")
        {
            if (!ParserOperando.TryParse(def.A ?? "", out var opA, out var errA)) return (false, $"{def.Op} FALHOU (parse 'a': {errA})");
            if (!ParserOperando.TryParse(def.Min ?? "", out var opMin, out var errMin)) return (false, $"{def.Op} FALHOU (parse 'min': {errMin})");
            if (!ParserOperando.TryParse(def.Max ?? "", out var opMax, out var errMax)) return (false, $"{def.Op} FALHOU (parse 'max': {errMax})");

            var va = ctx.ResolverOperando(opA);
            var vmin = ctx.ResolverOperando(opMin);
            var vmax = ctx.ResolverOperando(opMax);

            if (va is null || vmin is null || vmax is null) return (false, $"{def.Op} FALHOU (valor nulo)");

            result = va >= vmin && va <= vmax;
            detalhe = $"({va:F2} em [{vmin:F2}, {vmax:F2}])";
            return (result, result ? $"{def.Op} OK {detalhe}".TrimEnd() : $"{def.Op} FALHOU {detalhe}".TrimEnd());
        }

        if (!ParserOperando.TryParse(def.A ?? "", out var binOpA, out var errBinA))
            return (false, $"{def.Op} FALHOU (parse 'a': {errBinA})");
        if (!ParserOperando.TryParse(def.B ?? "", out var binOpB, out var errBinB))
            return (false, $"{def.Op} FALHOU (parse 'b': {errBinB})");

        var a = ctx.ResolverOperando(binOpA);
        var b = ctx.ResolverOperando(binOpB);

        if (a is null || b is null) return (false, $"{def.Op} FALHOU (valor nulo)");

        switch (def.Op)
        {
            case ">": result = a > b; detalhe = $"({a:F2} > {b:F2})"; break;
            case ">=": result = a >= b; detalhe = $"({a:F2} >= {b:F2})"; break;
            case "<": result = a < b; detalhe = $"({a:F2} < {b:F2})"; break;
            case "<=": result = a <= b; detalhe = $"({a:F2} <= {b:F2})"; break;
            case "==": result = Math.Abs(a.Value - b.Value) < 0.00001; detalhe = $"({a:F2} == {b:F2})"; break;
            case "pertoDe":
                var tol = def.ToleranciaPreco ?? 0;
                result = Math.Abs(a.Value - b.Value) <= tol;
                detalhe = $"(dist {Math.Abs(a.Value - b.Value):F2} <= {tol})";
                break;
            case "distanciaMinima":
                var dmin = def.ValorPreco ?? 0;
                result = Math.Abs(a.Value - b.Value) >= dmin;
                detalhe = $"(dist {Math.Abs(a.Value - b.Value):F2} >= {dmin})";
                break;
            case "distanciaMaxima":
                var dmax = def.ValorPreco ?? 0;
                result = Math.Abs(a.Value - b.Value) <= dmax;
                detalhe = $"(dist {Math.Abs(a.Value - b.Value):F2} <= {dmax})";
                break;
            case "cruzouAcima":
            case "cruzouAbaixo":
                var a1 = ctx.ResolverOperando(binOpA! with { Indice = (binOpA!.Indice ?? 0) + 1 });
                var b1 = ctx.ResolverOperando(binOpB! with { Indice = (binOpB!.Indice ?? 0) + 1 });
                if (a1 is null || b1 is null) return (false, $"{def.Op} FALHOU (histórico insuficiente)");

                if (def.Op == "cruzouAcima")
                {
                    result = a1 <= b1 && a > b;
                    detalhe = $"([N-1: {a1:F4} <= {b1:F4}] -> [Atual: {a:F4} > {b:F4}])";
                }
                else
                {
                    result = a1 >= b1 && a < b;
                    detalhe = $"([N-1: {a1:F4} >= {b1:F4}] -> [Atual: {a:F4} < {b:F4}])";
                }
                break;
            default:
                return (false, $"{def.Op} FALHOU (operador não implementado)");
        }

        return (result, result ? $"{def.Op} OK {detalhe}".TrimEnd() : $"{def.Op} FALHOU {detalhe}".TrimEnd());
    }
}
