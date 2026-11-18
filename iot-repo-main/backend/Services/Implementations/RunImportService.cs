
namespace Luggage;

public class RunImportService(IAirportRepository airports, IDeviceRepository devices, IFlightRepository flights, IJourneyRepository journeys, IAssignmentRepository assignments, IUnitOfWork work, ProcessingGate gate, IDashboardNotifier notifications, IEventRepository events, ITripRepository trips) : IRunImportService
{
    public async Task Import(RunImport run)
    {
        if (string.IsNullOrWhiteSpace(run.SimulationRunId) || run.StartAt == default || run.StartAt.Offset != TimeSpan.Zero ||
            run.Flights.Length == 0 || run.Journeys.Length == 0) throw new ArgumentException("Invalid run manifest");
        if (run.Airports.Any(a => a.RegionId != "EU" || !AirportValidation.IsValid(a))) throw new ArgumentException("Invalid airport metadata/region");
        if (run.Flights.Any(f => f.RunId != run.SimulationRunId || string.IsNullOrWhiteSpace(f.Id) || f.Origin == f.Destination ||
            f.ActualDeparture != null || f.ScheduledDeparture < run.StartAt || f.ScheduledDeparture.Offset != TimeSpan.Zero)) throw new ArgumentException("Invalid flight");
        if (run.Journeys.Any(j => j.RunId != run.SimulationRunId || string.IsNullOrWhiteSpace(j.Id) || string.IsNullOrWhiteSpace(j.LuggageId)))
            throw new ArgumentException("Invalid journey");
        if (run.Flights.Select(f => f.Id).Distinct().Count() != run.Flights.Length ||
            run.Journeys.Select(j => j.Id).Distinct().Count() != run.Journeys.Length ||
            run.Assignments.Length != run.Journeys.Length || run.Assignments.Select(a => a.JourneyId).Distinct().Count() != run.Journeys.Length ||
            run.Assignments.Any(a => a.LoadedAt != null || !run.Journeys.Any(j => j.Id == a.JourneyId) || !run.Flights.Any(f => f.Id == a.FlightId)))
            throw new ArgumentException("Invalid/duplicate assignments or identifiers");
        // Older manifests remain independent single-leg trips.
        foreach (var journey in run.Journeys)
            if (string.IsNullOrWhiteSpace(journey.TripId)) journey.TripId = journey.Id;
        var flightById = run.Flights.ToDictionary(x => x.Id);
        var assignmentByJourney = run.Assignments.ToDictionary(x => x.JourneyId);
        var importedTrips = new List<Trip>();
        foreach (var group in run.Journeys.GroupBy(x => x.TripId))
        {
            var legs = group.OrderBy(x => x.LegOrder).ToArray();
            if (legs.Select(x => x.LuggageId).Distinct().Count() != 1 ||
                !legs.Select(x => x.LegOrder).SequenceEqual(Enumerable.Range(1, legs.Length)))
                throw new ArgumentException("Trip legs must share luggage and have consecutive order starting at one");
            for (var i = 1; i < legs.Length; i++)
            {
                var previous = flightById[assignmentByJourney[legs[i - 1].Id].FlightId];
                var next = flightById[assignmentByJourney[legs[i].Id].FlightId];
                if (previous.Destination != next.Origin || previous.ScheduledDeparture >= next.ScheduledDeparture)
                    throw new ArgumentException("Trip flights must connect in departure order");
            }
            importedTrips.Add(new Trip { Id = group.Key, RunId = run.SimulationRunId, LuggageId = legs[0].LuggageId });
        }
        await gate.Mutex.WaitAsync();
        try
        {
            if (await events.IsRetiredRunAsync(run.SimulationRunId)) throw new ArgumentException("Run was cleared; start a new simulation instead");
            if (await flights.HasRunAsync(run.SimulationRunId)) throw new ArgumentException("Run already imported; publish the existing prepared file");
            await using var tx = await work.BeginTransactionAsync();
            foreach (var a in run.Airports)
            {
                var existing = await airports.GetAsync(a.Id);
                if (existing == null) airports.Add(a);
                else if (existing.RegionId != a.RegionId || !existing.Enabled) throw new ArgumentException("Airport region/registration conflict");
            }
            await work.SaveChangesAsync();
            foreach (var d in run.Devices)
            {
                if (!await airports.IsEnabledAsync(d.AirportId) || string.IsNullOrWhiteSpace(d.Id) || d.Type is not ("scanner" or "gate"))
                    throw new ArgumentException("Invalid device");
                var existing = await devices.GetAsync(d.AirportId, d.Id);
                if (existing == null) devices.Add(d);
                else if (existing.Type != d.Type || !existing.Enabled) throw new ArgumentException("Device registration conflict");
            }
            foreach (var f in run.Flights)
                if (!await airports.IsEnabledAsync(f.Origin) || !await airports.IsEnabledAsync(f.Destination))
                    throw new ArgumentException("Unknown flight airport");
            trips.AddRange(importedTrips);
            flights.AddRange(run.Flights); journeys.AddRange(run.Journeys); assignments.AddRange(run.Assignments);
            await work.SaveChangesAsync(); await tx.CommitAsync();
        }
        catch (PersistenceConflictException) { throw new ConflictException("Manifest conflicts with stored identities"); }
        finally { gate.Mutex.Release(); }
        await notifications.Send();
    }
}

