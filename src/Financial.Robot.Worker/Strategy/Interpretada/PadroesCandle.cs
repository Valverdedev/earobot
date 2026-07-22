using Financial.Robot.Domain.Events;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public static class PadroesCandle
{
    public static bool Avaliar(CondicaoDef def, ContextoAvaliacao ctx, out string rastro)
    {
        var idx = def.Candle ?? 0;
        var c = ctx.ObterCandle(idx);
        var cAnt = ctx.ObterCandle(idx + 1);

        if (c is null)
        {
            rastro = $"{def.Padrao} FALHOU (histórico insuficiente)";
            return false;
        }

        var range = c.Maximo - c.Minimo;
        var corpo = c.Fechamento - c.Abertura;
        var corpoAbs = Math.Abs(corpo);
        var pavioSuperior = Math.Max(c.Abertura, c.Fechamento) < c.Maximo ? c.Maximo - Math.Max(c.Abertura, c.Fechamento) : 0;
        var pavioInferior = Math.Min(c.Abertura, c.Fechamento) > c.Minimo ? Math.Min(c.Abertura, c.Fechamento) - c.Minimo : 0;

        bool result = false;
        string detalhe = "";

        switch (def.Padrao)
        {
            case "trendBarAlta":
                {
                    var limiteFracao = def.CorpoMinimoFracaoRange ?? 0.55;
                    result = range > 0 && corpo > 0 && (corpo / range) >= limiteFracao;
                    detalhe = $"(corpo/range = {(range > 0 ? (corpo / range) : 0):F2})";
                }
                break;
            case "trendBarBaixa":
                {
                    var limiteFracao = def.CorpoMinimoFracaoRange ?? 0.55;
                    result = range > 0 && corpo < 0 && (Math.Abs(corpo) / range) >= limiteFracao;
                    detalhe = $"(corpo/range = {(range > 0 ? (Math.Abs(corpo) / range) : 0):F2})";
                }
                break;
            case "rejeicaoCompradora":
                {
                    var multPavio = def.MultiploPavio ?? 2.0;
                    result = pavioInferior >= (corpoAbs * multPavio) && corpo > 0;
                    detalhe = $"(pavioInf={pavioInferior:F0}, corpo={corpoAbs:F0})";
                }
                break;
            case "rejeicaoVendedora":
                {
                    var multPavio = def.MultiploPavio ?? 2.0;
                    result = pavioSuperior >= (corpoAbs * multPavio) && corpo < 0;
                    detalhe = $"(pavioSup={pavioSuperior:F0}, corpo={corpoAbs:F0})";
                }
                break;
            case "fechamentoDirecional":
                {
                    if (def.Lado == "alta") result = corpo > 0;
                    else if (def.Lado == "baixa") result = corpo < 0;
                    detalhe = $"(corpo={corpo:F0})";
                }
                break;
            case "sequencia":
                {
                    if (string.IsNullOrWhiteSpace(def.Lado))
                    {
                        rastro = $"{def.Padrao} FALHOU (lado não especificado)";
                        return false;
                    }

                    var n = def.Candles ?? 1;
                    result = true;
                    int count = 0;
                    for (int i = 0; i < n; i++)
                    {
                        var curr = ctx.ObterCandle(idx + i);
                        if (curr is null) { result = false; break; }
                        var cBody = curr.Fechamento - curr.Abertura;
                        if (def.Lado == "alta" && cBody <= 0) { result = false; break; }
                        if (def.Lado == "baixa" && cBody >= 0) { result = false; break; }
                        count++;
                    }
                    detalhe = $"(lidas {count}/{n})";
                }
                break;
            case "insideBar":
                {
                    if (cAnt is null) { result = false; detalhe = "(sem anterior)"; }
                    else
                    {
                        result = c.Maximo <= cAnt.Maximo && c.Minimo >= cAnt.Minimo;
                        detalhe = $"(M {c.Maximo}<=M {cAnt.Maximo}, m {c.Minimo}>=m {cAnt.Minimo})";
                    }
                }
                break;
            case "outsideBar":
                {
                    if (cAnt is null) { result = false; detalhe = "(sem anterior)"; }
                    else
                    {
                        result = c.Maximo > cAnt.Maximo && c.Minimo < cAnt.Minimo;
                    }
                }
                break;
            case "engolfoAlta":
                {
                    if (cAnt is null) { result = false; detalhe = "(sem anterior)"; }
                    else
                    {
                        var corpoAnt = cAnt.Fechamento - cAnt.Abertura;
                        result = corpoAnt < 0 && corpo > 0 && c.Fechamento > cAnt.Abertura && c.Abertura < cAnt.Fechamento;
                    }
                }
                break;
            case "engolfoBaixa":
                {
                    if (cAnt is null) { result = false; detalhe = "(sem anterior)"; }
                    else
                    {
                        var corpoAnt = cAnt.Fechamento - cAnt.Abertura;
                        result = corpoAnt > 0 && corpo < 0 && c.Fechamento < cAnt.Abertura && c.Abertura > cAnt.Fechamento;
                    }
                }
                break;
            default:
                rastro = $"{def.Padrao} FALHOU (padrão não implementado)";
                return false;
        }

        rastro = result ? $"{def.Padrao} OK {detalhe}".TrimEnd() : $"{def.Padrao} FALHOU {detalhe}".TrimEnd();
        return result;
    }
}
