namespace AirportSimulation;

public interface IDeviceCommandClient
{
    Task Execute(DeviceActionCommand command, CancellationToken token);
}
