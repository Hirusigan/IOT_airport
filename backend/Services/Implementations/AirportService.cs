namespace Luggage;

public class AirportService(IAirportRepository airports, IDeviceRepository devices, IAlertRepository alerts, IFlightRepository flights, IUnitOfWork work,
    BrokerHealth health, RuleSettings settings, ProcessingGate gate, IDashboardNotifier notifications, TimeProvider clock, IAirportBrokerProvisioner brokers) : IAirportService
{
    private async Task<AirportDTO> View(Airport airport)
    {
        var activeAlerts = await alerts.GetFilteredAsync(airport.Id, true);
        var status = activeAlerts.Any(x => x.Severity == AlertSeverity.Red) ? "red" : activeAlerts.Count > 0 ? "orange" : "green";
        var telemetry = !health.Connected ? "disconnected" : airport.LastSeenAt == null ? "unknown" :
            clock.GetUtcNow() - airport.LastSeenAt > TimeSpan.FromSeconds(settings.StaleSeconds) ? "stale" : "healthy";
        return new(airport.Id, airport.Name, airport.Latitude, airport.Longitude, airport.RegionId, airport.LastSeenAt, status, activeAlerts.Count, telemetry);
    }
    public async Task<IReadOnlyList<AirportDTO>> GetAll()
    {
        var result = new List<AirportDTO>();
        foreach (var airport in await airports.GetEnabledAsync()) result.Add(await View(airport));
        return result;
    }
    public async Task<AirportDetailsDTO?> GetDetails(string id)
    {
        var airport = await airports.GetAsync(id);
        return airport == null ? null : new(await View(airport), await flights.GetByAirportAsync(id),
            (await alerts.GetFilteredAsync(id)).Select(AlertDTO.From).ToList());
    }
    public async Task<AirportDTO> Register(Airport airport)
    {
        if (!AirportValidation.IsValid(airport) || airport.RegionId != "EU" || !airport.Enabled || airport.LastSeenAt != null)
            throw new ArgumentException("Valid metadata in enabled EU region required");
        await gate.Mutex.WaitAsync();
        try
        {
            if (await airports.GetAsync(airport.Id) != null) throw new ConflictException("Airport already exists");
            await brokers.Provision(airport.Id);
            airports.Add(airport); await work.SaveChangesAsync();
        }
        finally { gate.Mutex.Release(); }
        await notifications.Send();
        return await View(airport);
    }
    public async Task RegisterSchedule(FlightSchedule schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.SimulationRunId) || schedule.Flights.Length == 0 ||
            schedule.Flights.Any(f => string.IsNullOrWhiteSpace(f.Id) || f.ScheduledDeparture == default ||
                f.ScheduledDeparture.Offset != TimeSpan.Zero ||
                (f.ScheduledArrival != null && (f.ScheduledArrival.Value.Offset != TimeSpan.Zero || f.ScheduledArrival.Value <= f.ScheduledDeparture)) ||
                f.Origin == f.Destination) ||
            schedule.Flights.Select(f => f.Id).Distinct().Count() != schedule.Flights.Length)
            throw new ArgumentException("Invalid flight schedule");
        await gate.Mutex.WaitAsync();
        try
        {
            foreach (var d in schedule.Devices ?? [])
            {
                if (!await airports.IsEnabledAsync(d.AirportId) || string.IsNullOrWhiteSpace(d.Id) ||
                    d.Id.Contains('/') || d.Id.Contains('+') || d.Id.Contains('#') || d.Type is not ("scanner" or "gate"))
                    throw new ArgumentException("Invalid schedule device");
                var existingDevice = await devices.GetAsync(d.AirportId, d.Id);
                if (existingDevice == null) devices.Add(new Device { Id = d.Id, AirportId = d.AirportId, Type = d.Type });
                else if (!existingDevice.Enabled || existingDevice.Type != d.Type) throw new ConflictException("Schedule device conflicts with registration");
            }
            foreach (var f in schedule.Flights)
            {
                if (!await airports.IsEnabledAsync(f.Origin) || !await airports.IsEnabledAsync(f.Destination))
                    throw new ArgumentException("Unknown flight airport");
                var existing = await flights.GetAsync(f.Id);
                if (existing != null)
                {
                    if (existing.RunId != schedule.SimulationRunId || existing.Number != f.Number || existing.Origin != f.Origin ||
                        existing.Destination != f.Destination || existing.ScheduledDeparture != f.ScheduledDeparture ||
                        existing.ScheduledArrival != f.ScheduledArrival)
                        throw new ConflictException("Flight schedule conflicts with stored identity");
                    continue;
                }
                flights.Add(new Flight
                {
                    Id = f.Id,
                    RunId = schedule.SimulationRunId,
                    Number = f.Number,
                    Origin = f.Origin,
                    Destination = f.Destination,
                    ScheduledDeparture = f.ScheduledDeparture,
                    ScheduledArrival = f.ScheduledArrival
                });
            }
            await work.SaveChangesAsync();
        }
        finally { gate.Mutex.Release(); }
        await notifications.Send();
    }
    public async Task RecordDeparture(string flightId, FlightDepartureUpdate departure)
    {
        if (string.IsNullOrWhiteSpace(departure.SimulationRunId) || departure.DepartedAt == default || departure.DepartedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid flight departure");
        await gate.Mutex.WaitAsync();
        try
        {
            var flight = await flights.GetAsync(flightId);
            if (flight == null || flight.RunId != departure.SimulationRunId) throw new ArgumentException("Unknown flight/run");
            if (flight.ActualDeparture != null && flight.ActualDeparture != departure.DepartedAt)
                throw new ConflictException("Conflicting departure evidence");
            flight.ActualDeparture = departure.DepartedAt;
            await work.SaveChangesAsync();
        }
        finally { gate.Mutex.Release(); }
        await notifications.Send();
    }
    public async Task RecordArrival(string flightId, FlightArrivalUpdate arrival)
    {
        if (string.IsNullOrWhiteSpace(arrival.SimulationRunId) || arrival.ArrivedAt == default || arrival.ArrivedAt.Offset != TimeSpan.Zero)
            throw new ArgumentException("Invalid flight arrival");
        await gate.Mutex.WaitAsync();
        try
        {
            var flight = await flights.GetAsync(flightId);
            if (flight == null || flight.RunId != arrival.SimulationRunId) throw new ArgumentException("Unknown flight/run");
            if (flight.ActualDeparture == null || arrival.ArrivedAt < flight.ActualDeparture)
                throw new ArgumentException("Flight arrival requires a preceding departure");
            if (flight.ActualArrival != null && flight.ActualArrival != arrival.ArrivedAt)
                throw new ConflictException("Conflicting arrival evidence");
            flight.ActualArrival = arrival.ArrivedAt;
            await work.SaveChangesAsync();
        }
        finally { gate.Mutex.Release(); }
        await notifications.Send();
    }
}
