using System.Text.Json;

namespace Financial.Robot.Application.Automation;

/// <summary>Valida envelopes externos antes de acionar Hermes ou aplicar configurações.</summary>
public static class AutomationContractValidator
{
    private static readonly HashSet<string> TriggerTypes =
    [
        "schedule", "manual", "order_opened", "order_closed", "position_updated",
        "price_above", "price_below", "level_touched", "spread_changed",
        "volatility_changed", "trend_changed", "drawdown_warning", "daily_target_reached",
        "daily_loss_reached", "market_open", "market_close"
    ];

    private static readonly HashSet<string> ApplyOperations =
    [
        ConfigApplyOperations.Replace,
        ConfigApplyOperations.Merge
    ];

    /// <summary>Valida uma extração de mercado recebida pela Bridge.Api.</summary>
    public static IReadOnlyList<string> ValidarExtracao(MarketExtractionEnvelope envelope)
    {
        var erros = new List<string>();
        ValidarSchema(envelope.SchemaVersion, erros);

        if (string.IsNullOrWhiteSpace(envelope.ExtractionId))
            erros.Add("extractionId é obrigatório.");

        if (string.IsNullOrWhiteSpace(envelope.Source))
            erros.Add("source é obrigatório.");

        ValidarTarget(envelope.Target, erros);
        ValidarTrigger(envelope.Trigger, erros);

        if (envelope.MarketContext.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            erros.Add("marketContext é obrigatório.");

        if (envelope.Raw.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            erros.Add("raw é obrigatório.");

        return erros;
    }

    /// <summary>Valida um pedido de aplicação automática antes de gravar arquivos locais.</summary>
    public static IReadOnlyList<string> ValidarApply(ConfigApplyEnvelope envelope)
    {
        var erros = new List<string>();
        ValidarSchema(envelope.SchemaVersion, erros);

        if (string.IsNullOrWhiteSpace(envelope.MessageId))
            erros.Add("messageId é obrigatório.");

        if (string.IsNullOrWhiteSpace(envelope.CorrelationId))
            erros.Add("correlationId é obrigatório.");

        if (envelope.Type != AutomationMessageTypes.ConfigApply)
            erros.Add($"type deve ser '{AutomationMessageTypes.ConfigApply}'.");

        ValidarTarget(envelope.Target, erros);

        if (envelope.ApplyMode is not ConfigApplyModes.Automatic and not ConfigApplyModes.Manual)
            erros.Add("applyMode deve ser 'automatic' ou 'manual'.");

        if (envelope.Files.Count == 0)
            erros.Add("files deve conter ao menos um arquivo.");

        for (var i = 0; i < envelope.Files.Count; i++)
            ValidarArquivo(envelope.Files[i], $"files[{i}]", erros);

        return erros;
    }

    private static void ValidarSchema(string schemaVersion, List<string> erros)
    {
        if (schemaVersion != "1.0")
            erros.Add("schemaVersion deve ser '1.0'.");
    }

    private static void ValidarTarget(AutomationTarget target, List<string> erros)
    {
        if (string.IsNullOrWhiteSpace(target.TerminalId))
            erros.Add("target.terminalId é obrigatório.");

        if (string.IsNullOrWhiteSpace(target.Symbol))
            erros.Add("target.symbol é obrigatório.");

        if (target.Environment is not "simulation" and not "paper" and not "live")
            erros.Add("target.environment deve ser simulation, paper ou live.");
    }

    private static void ValidarTrigger(AutomationTrigger trigger, List<string> erros)
    {
        if (string.IsNullOrWhiteSpace(trigger.Type))
        {
            erros.Add("trigger.type é obrigatório.");
            return;
        }

        if (!TriggerTypes.Contains(trigger.Type))
            erros.Add($"trigger.type desconhecido: {trigger.Type}.");

        if (trigger.Type is "price_above" or "price_below" or "level_touched")
        {
            if (!trigger.Price.HasValue || !trigger.Threshold.HasValue)
                erros.Add($"trigger.{trigger.Type} exige price e threshold.");
        }

        if (trigger.Type == "order_closed" && !trigger.Ticket.HasValue)
            erros.Add("trigger.order_closed exige ticket.");
    }

    private static void ValidarArquivo(ConfigApplyFile file, string path, List<string> erros)
    {
        if (string.IsNullOrWhiteSpace(file.Path))
        {
            erros.Add($"{path}.path é obrigatório.");
            return;
        }

        if (!IsConfigPathSeguro(file.Path))
            erros.Add($"{path}.path deve apontar para config/*.config.json ou config/*.estrategia.json sem path traversal.");

        if (!ApplyOperations.Contains(file.Operation))
            erros.Add($"{path}.operation deve ser replace ou merge.");

        if (file.Content.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
            erros.Add($"{path}.content é obrigatório.");

        if (file.Path.EndsWith(".estrategia.json", StringComparison.OrdinalIgnoreCase))
            ValidarEstrategiaTemSchema(file.Content, path, erros);
    }

    /// <summary>Retorna true se o caminho é relativo e limitado à pasta config.</summary>
    public static bool IsConfigPathSeguro(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return false;
        if (Path.IsPathRooted(relativePath)) return false;

        var normalized = relativePath.Replace('\\', '/');
        if (normalized.Contains("//", StringComparison.Ordinal)) return false;
        if (normalized.Split('/').Any(part => part is ".." or "." or "")) return false;
        if (!normalized.StartsWith("config/", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalized.Contains("/.backups/", StringComparison.OrdinalIgnoreCase)) return false;

        return normalized.EndsWith(".config.json", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".estrategia.json", StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidarEstrategiaTemSchema(JsonElement content, string path, List<string> erros)
    {
        if (content.ValueKind != JsonValueKind.Object)
        {
            erros.Add($"{path}.content deve ser objeto JSON.");
            return;
        }

        if (!content.TryGetProperty("$schemaVersion", out var schema) || schema.GetString() != "1.0")
            erros.Add($"{path}.content deve conter '$schemaVersion': '1.0'.");
    }
}
