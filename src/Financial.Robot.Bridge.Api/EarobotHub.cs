using Financial.Robot.Application.Automation;
using Microsoft.AspNetCore.SignalR;

namespace Financial.Robot.Bridge.Api;

public sealed class EarobotHub : Hub
{
    private readonly IAutomationStore _store;

    public EarobotHub(IAutomationStore store)
    {
        _store = store;
    }

    public override async Task OnConnectedAsync()
    {
        var terminalId = Context.GetHttpContext()?.Request.Query["terminalId"].ToString();
        if (!string.IsNullOrWhiteSpace(terminalId))
            await Groups.AddToGroupAsync(Context.ConnectionId, terminalId);

        await base.OnConnectedAsync();
    }

    public Task ConfigApplyAck(ConfigApplyAckEnvelope ack)
    {
        _store.SaveAck(ack);
        return Task.CompletedTask;
    }
}
