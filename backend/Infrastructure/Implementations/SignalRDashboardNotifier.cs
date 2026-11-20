using Microsoft.AspNetCore.SignalR;
namespace Luggage;

public class SignalRDashboardNotifier(IHubContext<DashboardHub> hub) : IDashboardNotifier
{
    public async Task Send(string? journeyId = null)
    {
        await hub.Clients.All.SendAsync("AirportStatusChanged");
        await hub.Clients.All.SendAsync("AlertChanged");
        if (journeyId != null)
            await hub.Clients.All.SendAsync("LuggageUpdated", journeyId);
    }

    public Task SendSimulationStatus(SimulationStatusDTO status) =>
        hub.Clients.All.SendAsync("SimulationStatusChanged", status);
}
