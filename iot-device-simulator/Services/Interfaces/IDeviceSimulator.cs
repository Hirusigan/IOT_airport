namespace IotDeviceSimulator;

public interface IDeviceSimulator
{
    Task Execute(DeviceActionCommand command, CancellationToken token);
}
