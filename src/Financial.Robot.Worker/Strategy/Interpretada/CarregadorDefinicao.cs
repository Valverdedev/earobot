using System.Text.Json;
using System.Text.Json.Serialization;

namespace Financial.Robot.Worker.Strategy.Interpretada;

public static class CarregadorDefinicao
{
    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public static string? ObterCaminhoCompleto(string caminhoRelativo)
    {
        var basePath = Directory.GetCurrentDirectory();
        var fullPath = Path.GetFullPath(Path.Combine(basePath, "config", caminhoRelativo));
        if (File.Exists(fullPath)) return fullPath;
        
        fullPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "config", caminhoRelativo));
        if (File.Exists(fullPath)) return fullPath;
        
        return null;
    }

    public static (DefinicaoEstrategia? Definicao, IReadOnlyList<string> Erros) Carregar(string caminhoRelativo)
    {
        try
        {
            var basePath = Directory.GetCurrentDirectory();
            var fullPath = Path.GetFullPath(Path.Combine(basePath, "config", caminhoRelativo));

            if (!File.Exists(fullPath))
            {
                fullPath = Path.GetFullPath(Path.Combine(basePath, "..", "..", "config", caminhoRelativo));
            }

            if (!File.Exists(fullPath))
            {
                return (null, [$"Arquivo não encontrado: {caminhoRelativo}"]);
            }

            var json = File.ReadAllText(fullPath);
            var def = JsonSerializer.Deserialize<DefinicaoEstrategia>(json, _options);

            if (def is null)
                return (null, ["O arquivo JSON está vazio ou inválido."]);

            var erros = ValidadorDefinicao.Validar(def);
            if (erros.Count > 0)
                return (null, erros);

            return (def, []);
        }
        catch (JsonException ex)
        {
            return (null, [$"Erro de parse JSON: {ex.Message}"]);
        }
        catch (Exception ex)
        {
            return (null, [$"Erro ao carregar arquivo: {ex.Message}"]);
        }
    }
}
