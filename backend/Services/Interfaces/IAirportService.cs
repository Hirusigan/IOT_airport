namespace Luggage;

public interface IAirportService
{
    Task<IReadOnlyList<AirportDTO>> GetAll();
    Task<AirportDetailsDTO?> GetDetails(string id);
    Task<AirportDTO> Register(Airport airport);
    Task RegisterSchedule(FlightSchedule schedule);
    Task RecordDeparture(string flightId, FlightDepartureUpdate departure);
    Task RecordArrival(string flightId, FlightArrivalUpdate arrival);
}
