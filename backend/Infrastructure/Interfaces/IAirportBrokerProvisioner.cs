namespace Luggage;

public interface IAirportBrokerProvisioner
{
    Task Provision(string airportId);
}
