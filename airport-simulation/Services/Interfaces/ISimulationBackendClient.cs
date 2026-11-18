namespace AirportSimulation;

public interface ISimulationBackendClient
{
    Task RegisterSchedule(FlightScheduleRequest schedule, CancellationToken token);
    Task RecordDeparture(string flightId, FlightDepartureRequest departure, CancellationToken token);
    Task RecordArrival(string flightId, FlightArrivalRequest arrival, CancellationToken token);
}
