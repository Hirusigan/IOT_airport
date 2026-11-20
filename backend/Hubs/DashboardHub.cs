using Microsoft.AspNetCore.SignalR;
namespace Luggage;

public class DashboardHub(ISimulationRunner simulations) : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
        await Clients.Caller.SendAsync("SimulationStatusChanged", simulations.Status);
    }
}
