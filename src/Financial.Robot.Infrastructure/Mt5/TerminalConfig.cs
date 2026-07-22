namespace Financial.Robot.Infrastructure.Mt5;

public class TerminalConfig
{
    public string TerminalId { get; set; } = string.Empty;
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 8228;
    public bool SomenteLeitura { get; set; } = false;
}
