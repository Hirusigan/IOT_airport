using Microsoft.EntityFrameworkCore;

namespace Luggage;

public class AirportService(
    IAirportRepository airports,
    IDeviceRepository devices,
    IAlertRepository alerts,
    IFlightRepository flights,
    BrokerHealth health,
    RuleSettings settings,
    IDashboardNotifier notifications,
    TimeProvider clock,
    IAirportBrokerProvisioner brokers) : IAirportService
{
    private async Task<AirportDTO> View(Airport airport)
    {
        var activeAlerts = await alerts.GetFilteredAsync(airport.Id, true);
        var status = activeAlerts.Any(x => x.Severity == AlertSeverity.Red)
            ? "red" : activeAlerts.Count > 0 ? "orange" : "green";
            
        var telemetry = !health.Connected
            ? "disconnected"
            : airport.LastSeenAt == null
                ? "unknown"
                : clock.GetUtcNow() - airport.LastSeenAt > TimeSpan.FromSeconds(settings.StaleSeconds)
                    ? "stale"
                    : "healthy";

        return new(
            airport.Id,
            airport.Name,
            airport.Latitude,
            airport.Longitude,
            airport.RegionId,
            airport.LastSeenAt,
            status,
            activeAlerts.Count,
            telemetry);
    }

    public async Task<IReadOnlyList<AirportDTO>> GetAll()
    {
        var result = new List<AirportDTO>();
        foreach (var airport in await airports.GetEnabledAsync())
        {
            result.Add(await View(airport));
        }

        return result;
    }

    public async Task<AirportDetailsDTO?> GetDetails(string id)
    {
        var airport = await airports.GetAsync(id);
        return airport == null
            ? null
            : new AirportDetailsDTO(
                await View(airport),
                await flights.GetByAirportAsync(id),
                (await alerts.GetFilteredAsync(id)).Select(AlertDTO.From).ToList());
    }

    public async Task<AirportDTO> Register(Airport airport)
    {
        ValidateAirport(airport);
        if (await airports.GetAsync(airport.Id) != null)
        {
            throw new ConflictException("Airport already exists");
        }

        await brokers.Provision(airport.Id);

        try
        {
            await using var transaction = await airports.BeginTransactionAsync();
            if (await airports.GetAsync(airport.Id) != null)
            {
                throw new ConflictException("Airport already exists");
            }

            airports.Add(airport);
            await airports.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (PersistenceConflictException)
        {
            throw new ConflictException("Airport already exists");
        }

        await notifications.Send();
        return await View(airport);
    }

    public async Task RegisterSchedule(FlightSchedule schedule)
    {
        ValidateSchedule(schedule);

        try
        {
            await using var transaction = await airports.BeginTransactionAsync();
            await RegisterScheduleDevices(schedule);
            await RegisterScheduleFlights(schedule);
            await airports.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (PersistenceConflictException)
        {
            throw new ConflictException("Schedule conflicts with stored identities");
        }

        await notifications.Send();
    }

    private static void ValidateAirport(Airport airport)
    {
        if (!AirportValidation.IsValid(airport) || airport.RegionId != "EU" || !airport.Enabled || airport.LastSeenAt != null)
        {
            throw new ArgumentException("Valid metadata in enabled EU region required");
        }
    }

    private static void ValidateSchedule(FlightSchedule schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.SimulationRunId) || schedule.Flights.Length == 0 ||
            schedule.Flights.Any(f => string.IsNullOrWhiteSpace(f.Id) || f.ScheduledDeparture == default ||
                f.ScheduledDeparture.Offset != TimeSpan.Zero ||
                (f.ScheduledArrival != null &&
                    (f.ScheduledArrival.Value.Offset != TimeSpan.Zero || f.ScheduledArrival.Value <= f.ScheduledDeparture)) ||
                f.Origin == f.Destination) ||
            schedule.Flights.Select(f => f.Id).Distinct().Count() != schedule.Flights.Length)
        {
            throw new ArgumentException("Invalid flight schedule");
        }
    }

    private async Task RegisterScheduleDevices(FlightSchedule schedule)
    {
        foreach (var deviceUpdate in schedule.Devices ?? [])
        {
            if (!await airports.IsEnabledAsync(deviceUpdate.AirportId) || string.IsNullOrWhiteSpace(deviceUpdate.Id) ||
                deviceUpdate.Id.Contains('/') || deviceUpdate.Id.Contains('+') || deviceUpdate.Id.Contains('#'))
            {
                throw new ArgumentException("Invalid schedule device");
            }

            var deviceType = ModelValues.ParseDeviceType(deviceUpdate.Type);
            var existingDevice = await devices.GetAsync(deviceUpdate.AirportId, deviceUpdate.Id);
            if (existingDevice == null)
            {
                devices.Add(new Device
                {
                    Id = deviceUpdate.Id,
                    AirportId = deviceUpdate.AirportId,
                    Type = deviceType
                });
            }
            else if (!existingDevice.Enabled || existingDevice.Type != deviceType)
            {
                throw new ConflictException("Schedule device conflicts with registration");
            }
        }
    }

    private async Task RegisterScheduleFlights(FlightSchedule schedule)
    {
        foreach (var flightUpdate in schedule.Flights)
        {
            if (!await airports.IsEnabledAsync(flightUpdate.Origin) || !await airports.IsEnabledAsync(flightUpdate.Destination))
            {
                throw new ArgumentException("Unknown flight airport");
            }

            var existingFlight = await flights.GetAsync(flightUpdate.Id);
            if (existingFlight != null)
            {
                if (existingFlight.RunId != schedule.SimulationRunId || existingFlight.Number != flightUpdate.Number ||
                    existingFlight.Origin != flightUpdate.Origin || existingFlight.Destination != flightUpdate.Destination ||
                    existingFlight.ScheduledDeparture != flightUpdate.ScheduledDeparture ||
                    existingFlight.ScheduledArrival != flightUpdate.ScheduledArrival)
                {
                    throw new ConflictException("Flight schedule conflicts with stored identity");
                }

                continue;
            }

            flights.Add(new Flight
            {
                Id = flightUpdate.Id,
                RunId = schedule.SimulationRunId,
                Number = flightUpdate.Number,
                Origin = flightUpdate.Origin,
                Destination = flightUpdate.Destination,
                ScheduledDeparture = flightUpdate.ScheduledDeparture,
                ScheduledArrival = flightUpdate.ScheduledArrival
            });
        }
    }

    public async Task RecordDeparture(string flightId, FlightDepartureUpdate departure)
    {
        ValidateDeparture(departure);

        try
        {
            await using var transaction = await airports.BeginTransactionAsync();
            var flight = await GetFlightForRun(flightId, departure.SimulationRunId);
            if (flight.ActualDeparture != null && flight.ActualDeparture != departure.DepartedAt)
            {
                throw new ConflictException("Conflicting departure evidence");
            }

            flight.ActualDeparture = departure.DepartedAt;
            await airports.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Flight departure changed concurrently; retry the request");
        }
        catch (PersistenceConflictException)
        {
            throw new ConflictException("Flight departure changed concurrently; retry the request");
        }

        await notifications.Send();
    }

    public async Task RecordArrival(string flightId, FlightArrivalUpdate arrival)
    {
        ValidateArrival(arrival);

        try
        {
            await using var transaction = await airports.BeginTransactionAsync();
            var flight = await GetFlightForRun(flightId, arrival.SimulationRunId);
            if (flight.ActualDeparture == null || arrival.ArrivedAt < flight.ActualDeparture)
            {
                throw new ArgumentException("Flight arrival requires a preceding departure");
            }

            if (flight.ActualArrival != null && flight.ActualArrival != arrival.ArrivedAt)
            {
                throw new ConflictException("Conflicting arrival evidence");
            }

            flight.ActualArrival = arrival.ArrivedAt;
            await airports.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("Flight arrival changed concurrently; retry the request");
        }
        catch (PersistenceConflictException)
        {
            throw new ConflictException("Flight arrival changed concurrently; retry the request");
        }

        await notifications.Send();
    }

    private async Task<Flight> GetFlightForRun(string flightId, string runId)
    {
        var flight = await flights.GetAsync(flightId);
        if (flight == null || flight.RunId != runId)
        {
            throw new ArgumentException("Unknown flight/run");
        }

        return flight;
    }

    private static void ValidateDeparture(FlightDepartureUpdate departure)
    {
        if (string.IsNullOrWhiteSpace(departure.SimulationRunId) || departure.DepartedAt == default ||
            departure.DepartedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Invalid flight departure");
        }
    }

    private static void ValidateArrival(FlightArrivalUpdate arrival)
    {
        if (string.IsNullOrWhiteSpace(arrival.SimulationRunId) || arrival.ArrivedAt == default ||
            arrival.ArrivedAt.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Invalid flight arrival");
        }
    }
}
