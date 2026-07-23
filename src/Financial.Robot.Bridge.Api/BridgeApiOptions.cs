namespace Financial.Robot.Bridge.Api;

public sealed class BridgeApiOptions
{
    public bool UseFakeHermes { get; init; } = true;
    public bool RequireDeviceToken { get; init; }
    public string? DeviceToken { get; init; }
    public string HermesBaseUrl { get; init; } = "http://127.0.0.1:8642";
    public string? HermesApiKey { get; init; }
    public string HermesConversationPrefix { get; init; } = "earobot";
    public string HermesModel { get; init; } = "hermes-agent";
}
