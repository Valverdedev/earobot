using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Financial.Robot.Application.Automation;
using Microsoft.Extensions.Options;

namespace Financial.Robot.Bridge.Api;

public interface IHermesAnalysisClient
{
    Task<ConfigApplyEnvelope?> AnalyzeExtractionAsync(MarketExtractionEnvelope extraction, CancellationToken cancellationToken);
}

public sealed class FakeHermesAnalysisClient : IHermesAnalysisClient
{
    public Task<ConfigApplyEnvelope?> AnalyzeExtractionAsync(MarketExtractionEnvelope extraction, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        using var configDoc = JsonDocument.Parse($$"""
        {
          "operar": true,
          "automation": {
            "lastExtractionId": "{{JsonEscaper.Escape(extraction.ExtractionId)}}",
            "lastTrigger": "{{JsonEscaper.Escape(extraction.Trigger.Type)}}",
            "generatedAtUtc": "{{now:O}}"
          }
        }
        """);

        var envelope = new ConfigApplyEnvelope(
            "1.0",
            $"cfg-{extraction.ExtractionId}",
            extraction.ExtractionId,
            AutomationMessageTypes.ConfigApply,
            extraction.Target,
            [new ConfigApplyFile($"config/{extraction.Target.Symbol}.config.json", ConfigApplyOperations.Merge, configDoc.RootElement.Clone())],
            ConfigApplyModes.Automatic,
            new RollbackOptions(true, "auto"));

        return Task.FromResult<ConfigApplyEnvelope?>(envelope);
    }
}

public sealed class HttpHermesAnalysisClient : IHermesAnalysisClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient;
    private readonly BridgeApiOptions _options;
    private readonly ILogger<HttpHermesAnalysisClient> _logger;

    public HttpHermesAnalysisClient(HttpClient httpClient, IOptions<BridgeApiOptions> options, ILogger<HttpHermesAnalysisClient> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
        _httpClient.BaseAddress ??= new Uri(_options.HermesBaseUrl);
        if (!string.IsNullOrWhiteSpace(_options.HermesApiKey))
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.HermesApiKey);
    }

    public async Task<ConfigApplyEnvelope?> AnalyzeExtractionAsync(MarketExtractionEnvelope extraction, CancellationToken cancellationToken)
    {
        var prompt = $$"""
        Analise a extração de mercado abaixo e retorne somente JSON válido no contrato config.apply.
        O JSON deve usar schemaVersion 1.0, type config.apply, applyMode automatic, paths sob config/ e operar true quando a operação puder permanecer ativa.

        Extração:
        {{JsonSerializer.Serialize(extraction, JsonOptions)}}
        """;

        var payload = new
        {
            model = _options.HermesModel,
            conversation = $"{_options.HermesConversationPrefix}-{extraction.Target.Symbol}",
            input = prompt,
            instructions = "Retorne somente JSON válido, sem markdown."
        };

        using var response = await _httpClient.PostAsJsonAsync("/v1/responses", payload, JsonOptions, cancellationToken);
        response.EnsureSuccessStatusCode();
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var jsonText = ExtractJsonObject(text);
        if (jsonText is null)
        {
            _logger.LogWarning("Hermes não retornou um objeto JSON aplicável: {Response}", text);
            return null;
        }

        return JsonSerializer.Deserialize<ConfigApplyEnvelope>(jsonText, JsonOptions);
    }

    private static string? ExtractJsonObject(string text)
    {
        var first = text.IndexOf('{');
        var last = text.LastIndexOf('}');
        if (first < 0 || last <= first) return null;
        return text[first..(last + 1)];
    }
}

internal static class JsonEscaper
{
    public static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
