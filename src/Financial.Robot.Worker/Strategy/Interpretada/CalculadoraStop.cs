namespace Financial.Robot.Worker.Strategy.Interpretada;

public static class CalculadoraStop
{
    public static double? Calcular(StopDef? def, ContextoAvaliacao ctx, bool isCompra)
    {
        if (def is null) return null;

        double? valorBase = null;

        if (def.Tipo == "extremoCandle")
        {
            var c = ctx.ObterCandle(def.Candle ?? 0);
            if (c is not null)
            {
                if (def.Lado == "minimo") valorBase = c.Minimo;
                else if (def.Lado == "maximo") valorBase = c.Maximo;
            }
        }
        else if (def.Tipo == "valorOperando")
        {
            if (ParserOperando.TryParse(def.A ?? "", out var opA, out _))
            {
                valorBase = ctx.ResolverOperando(opA);
            }
        }
        else if (def.Tipo == "extremoRef")
        {
            if (!string.IsNullOrEmpty(def.Ref))
            {
                var c = ctx.ObterRef(def.Ref);
                if (c is not null)
                {
                    if (def.Lado == "minimo") valorBase = c.Minimo;
                    else if (def.Lado == "maximo") valorBase = c.Maximo;
                }
            }
        }
        else if (def.Tipo == "nivelProximo")
        {
            var op = new Operando { Fonte = def.TipoNivel == "suporte" ? TipoFonte.NivelSuportePreco : TipoFonte.NivelResistenciaPreco };
            valorBase = ctx.ResolverOperando(op);
        }
        
        if (valorBase is null) return null;

        var buffer = def.BufferPreco ?? 0;
        
        return isCompra ? valorBase - buffer : valorBase + buffer;
    }
}
