namespace Financial.Robot.Worker.Automation;

/// <summary>Status final da tentativa de aplicar uma atualização automática.</summary>
public enum ConfigApplyStatus
{
    Applied,
    Rejected,
    Failed
}

/// <summary>Resultado local da aplicação de um envelope config.apply.</summary>
public sealed record ConfigApplyResult(
    ConfigApplyStatus Status,
    string? BackupId,
    IReadOnlyList<string> Errors)
{
    public static ConfigApplyResult Applied(string? backupId) => new(ConfigApplyStatus.Applied, backupId, []);

    public static ConfigApplyResult Rejected(IReadOnlyList<string> errors) => new(ConfigApplyStatus.Rejected, null, errors);

    public static ConfigApplyResult Failed(string error, string? backupId = null) => new(ConfigApplyStatus.Failed, backupId, [error]);
}
