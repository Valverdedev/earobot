using System.Text.Json;

namespace Financial.Robot.Application.Automation;

/// <summary>Tipos de mensagem usados no pipeline Hermes → VPS → SignalR → robô.</summary>
public static class AutomationMessageTypes
{
    public const string AnalysisNoChange = "analysis.no_change";
    public const string StrategyProposal = "strategy.proposal";
    public const string ConfigUpdate = "config.update";
    public const string ConfigReplace = "config.replace";
    public const string ConfigApply = "config.apply";
    public const string ConfigApplyAck = "config.apply.ack";
}

/// <summary>Operações permitidas ao aplicar arquivos enviados pela Bridge.Api.</summary>
public static class ConfigApplyOperations
{
    public const string Replace = "replace";
    public const string Merge = "merge";
}

/// <summary>Modos de aplicação de uma atualização de configuração.</summary>
public static class ConfigApplyModes
{
    public const string Automatic = "automatic";
    public const string Manual = "manual";
}

/// <summary>Destino operacional de uma extração, análise ou atualização.</summary>
public sealed record AutomationTarget(
    string TerminalId,
    string Symbol,
    string? BrokerSymbol,
    string Environment,
    IReadOnlyList<string>? Timeframes);

/// <summary>Gatilho que originou uma nova extração de mercado.</summary>
public sealed record AutomationTrigger(
    string Type,
    string? Name = null,
    ulong? Ticket = null,
    string? Result = null,
    double? PnL = null,
    string? CloseReason = null,
    double? Price = null,
    double? Threshold = null,
    double? CurrentDrawdownPercent = null,
    string? From = null,
    string? To = null);

/// <summary>Envelope recebido pela VPS com uma nova extração de mercado para análise.</summary>
public sealed record MarketExtractionEnvelope(
    string SchemaVersion,
    string ExtractionId,
    string Source,
    DateTimeOffset CreatedAtUtc,
    AutomationTrigger Trigger,
    AutomationTarget Target,
    JsonElement MarketContext,
    JsonElement Raw);

/// <summary>Arquivo de configuração/estratégia que deve ser aplicado localmente.</summary>
public sealed record ConfigApplyFile(
    string Path,
    string Operation,
    JsonElement Content);

/// <summary>Opções de rollback para aplicação automática.</summary>
public sealed record RollbackOptions(
    bool Enabled,
    string? BackupId);

/// <summary>Envelope enviado pela Bridge.Api ao robô local para aplicar arquivos.</summary>
public sealed record ConfigApplyEnvelope(
    string SchemaVersion,
    string MessageId,
    string CorrelationId,
    string Type,
    AutomationTarget Target,
    IReadOnlyList<ConfigApplyFile> Files,
    string ApplyMode,
    RollbackOptions? Rollback);

/// <summary>ACK retornado pelo robô local depois de aplicar ou rejeitar uma atualização.</summary>
public sealed record ConfigApplyAckEnvelope(
    string SchemaVersion,
    string MessageId,
    string CorrelationId,
    string Type,
    string Status,
    DateTimeOffset ReceivedAtUtc,
    DateTimeOffset? AppliedAtUtc,
    string? BackupId,
    IReadOnlyList<AutomationValidationError>? Errors);

/// <summary>Erro de validação serializável para auditoria na VPS.</summary>
public sealed record AutomationValidationError(
    string Path,
    string Message);
