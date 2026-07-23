using System.Text.Json;
using System.Text.Json.Nodes;
using Financial.Robot.Application.Automation;
using Microsoft.Extensions.Logging;

namespace Financial.Robot.Worker.Automation;

/// <summary>Aplica envelopes config.apply gravando configs e estratégias na pasta config com backup prévio.</summary>
public sealed class ConfigApplyService
{
    private readonly ILogger<ConfigApplyService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public ConfigApplyService(ILogger<ConfigApplyService> logger)
    {
        _logger = logger;
    }

    /// <summary>Valida, faz backup e aplica os arquivos solicitados.</summary>
    public async Task<ConfigApplyResult> AplicarAsync(
        ConfigApplyEnvelope envelope,
        string rootPath,
        CancellationToken ct)
    {
        var erros = AutomationContractValidator.ValidarApply(envelope);
        if (erros.Count > 0)
        {
            _logger.LogWarning("[Automation] config.apply rejeitado: {Erros}", string.Join("; ", erros));
            return ConfigApplyResult.Rejected(erros);
        }

        var backupId = envelope.Rollback?.Enabled == true
            ? CriarBackupId(envelope)
            : null;

        try
        {
            var root = Path.GetFullPath(rootPath);
            foreach (var file in envelope.Files)
            {
                ct.ThrowIfCancellationRequested();

                var destino = ResolverDestinoSeguro(root, file.Path);
                if (destino is null)
                    return ConfigApplyResult.Rejected([$"Path inseguro: {file.Path}"]);

                if (backupId is not null && File.Exists(destino))
                    await CriarBackupAsync(root, file.Path, destino, backupId, ct);

                Directory.CreateDirectory(Path.GetDirectoryName(destino)!);

                if (file.Operation == ConfigApplyOperations.Merge && File.Exists(destino))
                    await AplicarMergeAsync(destino, file.Content, ct);
                else
                    await File.WriteAllTextAsync(destino, Serializar(file.Content), ct);

                _logger.LogInformation("[Automation] Arquivo aplicado: {Path} ({Operation})", file.Path, file.Operation);
            }

            return ConfigApplyResult.Applied(backupId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Automation] Falha ao aplicar config.apply {MessageId}", envelope.MessageId);
            return ConfigApplyResult.Failed(ex.Message, backupId);
        }
    }

    private static string? ResolverDestinoSeguro(string root, string relativePath)
    {
        if (!AutomationContractValidator.IsConfigPathSeguro(relativePath))
            return null;

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        var destino = Path.GetFullPath(Path.Combine(root, normalized));
        var configRoot = Path.GetFullPath(Path.Combine(root, "config"));

        return destino.StartsWith(configRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? destino
            : null;
    }

    private async Task CriarBackupAsync(
        string root,
        string relativePath,
        string destino,
        string backupId,
        CancellationToken ct)
    {
        var normalized = relativePath.Replace('\\', '/');
        var relativeInsideConfig = normalized["config/".Length..];
        var backupPath = Path.Combine(root, "config", ".backups", backupId, relativeInsideConfig);

        Directory.CreateDirectory(Path.GetDirectoryName(backupPath)!);
        await using var origem = File.OpenRead(destino);
        await using var backup = File.Create(backupPath);
        await origem.CopyToAsync(backup, ct);

        _logger.LogInformation("[Automation] Backup criado: {BackupPath}", backupPath);
    }

    private async Task AplicarMergeAsync(string destino, JsonElement patch, CancellationToken ct)
    {
        await using var stream = File.OpenRead(destino);
        var atual = await JsonNode.ParseAsync(stream, cancellationToken: ct) as JsonObject ?? [];
        var patchNode = JsonNode.Parse(patch.GetRawText()) as JsonObject ?? [];

        Merge(atual, patchNode);
        await File.WriteAllTextAsync(destino, atual.ToJsonString(_jsonOptions), ct);
    }

    private static void Merge(JsonObject destino, JsonObject patch)
    {
        foreach (var (key, value) in patch.ToList())
        {
            patch.Remove(key);

            if (value is JsonObject patchObj && destino[key] is JsonObject destinoObj)
            {
                Merge(destinoObj, patchObj);
                continue;
            }

            destino[key] = value;
        }
    }

    private string Serializar(JsonElement content)
    {
        var node = JsonNode.Parse(content.GetRawText());
        return node?.ToJsonString(_jsonOptions) ?? content.GetRawText();
    }

    private static string CriarBackupId(ConfigApplyEnvelope envelope)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
        return $"{timestamp}_{Sanitizar(envelope.CorrelationId)}";
    }

    private static string Sanitizar(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray();
        return new string(chars);
    }
}
