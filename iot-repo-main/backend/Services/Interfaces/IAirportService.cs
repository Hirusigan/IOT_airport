namespace Luggage;
public interface IAirportService
{
    Task<IReadOnlyList<AirportDTO>> GetAll();
    Task<AirportDetailsDTO?> GetDetails(string id);
    Task<AirportDTO> Register(Airport airport);
}
