using Financial.Robot.Domain.Interfaces;

namespace Financial.Robot.Application.Interfaces;

public interface IConnectionManager
{
    IGatewayMt5 GetClient(string terminalId);
    IEnumerable<string> GetConnectedTerminalIds();
    IReadOnlyDictionary<string, IGatewayMt5> GetAllGateways();
}
