namespace Financial.Robot.Worker.Automation;

/// <summary>Configuração da ponte SignalR com a Bridge.Api na VPS.</summary>
public sealed class AutomationBridgeOptions
{
    /// <summary>Habilita o cliente SignalR do robô local.</summary>
    public bool Enabled { get; init; }

    /// <summary>URL completa do hub, por exemplo https://vps/hubs/earobot.</summary>
    public string HubUrl { get; init; } = string.Empty;

    /// <summary>Token do dispositivo local usado no Authorization Bearer.</summary>
    public string DeviceToken { get; init; } = string.Empty;

    /// <summary>Diretório raiz onde a pasta config será alterada.</summary>
    public string ConfigRootPath { get; init; } = ".";
}
