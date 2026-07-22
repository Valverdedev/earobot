using System.Text.Json;

namespace Financial.Robot.Worker.Indicators;

public static class ParametroParser
{
    public static int ObterInt(IDictionary<string, object>? parametros, string chave, int padrao)
    {
        if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
            return padrao;

        return v switch
        {
            int i => i,
            long l => (int)l,
            double d => (int)d,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetInt32(out var jv) => jv,
            _ => Convert.ToInt32(v)
        };
    }

    public static double ObterDouble(IDictionary<string, object>? parametros, string chave, double padrao)
    {
        if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
            return padrao;

        return v switch
        {
            double d => d,
            int i => i,
            long l => l,
            JsonElement je when je.ValueKind == JsonValueKind.Number && je.TryGetDouble(out var jv) => jv,
            _ => Convert.ToDouble(v)
        };
    }

    public static string ObterString(IDictionary<string, object>? parametros, string chave, string padrao)
    {
        if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
            return padrao;

        return v switch
        {
            string s => s,
            JsonElement je when je.ValueKind == JsonValueKind.String => je.GetString() ?? padrao,
            _ => v.ToString() ?? padrao
        };
    }

    public static bool ObterBool(IDictionary<string, object>? parametros, string chave, bool padrao)
    {
        if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
            return padrao;

        return v switch
        {
            bool b => b,
            JsonElement je when je.ValueKind == JsonValueKind.True => true,
            JsonElement je when je.ValueKind == JsonValueKind.False => false,
            JsonElement je when je.ValueKind == JsonValueKind.String && bool.TryParse(je.GetString(), out var bv) => bv,
            string s when bool.TryParse(s, out var bs) => bs,
            _ => bool.TryParse(v.ToString(), out var bts) ? bts : padrao
        };
    }

    public static IReadOnlyList<(string Simbolo, double Peso)> ObterComponentes(IDictionary<string, object>? parametros, string chave = "componentes")
    {
        var result = new List<(string, double)>();
        if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
            return result;

        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                
                string? sim = null;
                double peso = 0;

                if (item.TryGetProperty("simbolo", out var propSim))
                    sim = propSim.GetString();

                if (item.TryGetProperty("peso", out var propPeso) && propPeso.ValueKind == JsonValueKind.Number)
                    propPeso.TryGetDouble(out peso);

                if (!string.IsNullOrEmpty(sim))
                    result.Add((sim, peso));
            }
        }

        return result;
    }

    public static (TimeSpan Inicio, TimeSpan Fim)? ObterJanelaHorario(IDictionary<string, object>? parametros, string chave)
    {
        var str = ObterString(parametros, chave, string.Empty);
        if (string.IsNullOrEmpty(str)) return null;
        var partes = str.Split('-');
        if (partes.Length == 2 && TimeOnly.TryParse(partes[0].Trim(), out var inicio) && TimeOnly.TryParse(partes[1].Trim(), out var fim))
            return (inicio.ToTimeSpan(), fim.ToTimeSpan());
        return null;
    }

    public static IReadOnlyList<(string Tipo, double Preco)> ObterNiveis(IDictionary<string, object>? parametros, string chave = "niveis")
    {
        var result = new List<(string, double)>();
        if (parametros == null || !parametros.TryGetValue(chave, out var v) || v is null)
            return result;

        if (v is JsonElement je && je.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in je.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;

                string? tipo = null;
                double preco = 0;

                if (item.TryGetProperty("tipo", out var propTipo))
                    tipo = propTipo.GetString();

                if (item.TryGetProperty("preco", out var propPreco) && propPreco.ValueKind == JsonValueKind.Number)
                    propPreco.TryGetDouble(out preco);

                if (!string.IsNullOrEmpty(tipo))
                    result.Add((tipo, preco));
            }
        }

        return result;
    }
}
