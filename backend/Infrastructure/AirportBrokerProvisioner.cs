using Docker.DotNet;
using Docker.DotNet.Models;
namespace Luggage;

public interface IAirportBrokerProvisioner { Task Provision(string airportId); }
public sealed class AirportBrokerProvisioner(IConfiguration config, ILogger<AirportBrokerProvisioner> log) : IAirportBrokerProvisioner
{
    const string Image = "eclipse-mosquitto:2.0.22";
    const string Network = "airport-brokers";
    const string Volume = "airport-broker-config";
    public async Task Provision(string airportId)
    {
        var root = config["Brokers:ConfigPath"] ?? "/broker-config";
        Directory.CreateDirectory(root);
        var confName = $"{airportId}.conf";
        var aclName = $"{airportId}.acl";
        await File.WriteAllTextAsync(Path.Combine(root, aclName), $"topic read airport/{airportId}/#\ntopic write airport/{airportId}/#\n");
        await File.WriteAllTextAsync(Path.Combine(root, confName),
            $"listener 1883 0.0.0.0\nallow_anonymous true\nacl_file /runtime/{aclName}\nconnection regional-{airportId}\naddress regional:1883\nlocal_clientid bridge-{airportId}\nremote_clientid bridge-{airportId}\ntry_private false\nnotifications false\ncleansession false\nrestart_timeout 1 30\ntopic airport/{airportId}/# out 1\n");
        using var client = new DockerClientConfiguration(new Uri(config["Docker:Endpoint"] ?? "unix:///var/run/docker.sock")).CreateClient();
        try { await client.Networks.CreateNetworkAsync(new NetworksCreateParameters { Name = Network, CheckDuplicate = true, Driver = "bridge" }); }
        catch (DockerApiException e) when (e.StatusCode == System.Net.HttpStatusCode.Conflict) { }
        try { await client.Images.CreateImageAsync(new ImagesCreateParameters { FromImage = "eclipse-mosquitto", Tag = "2.0.22" }, null, new Progress<JSONMessage>()); }
        catch (DockerApiException e) when (e.StatusCode == System.Net.HttpStatusCode.NotFound) { throw new InvalidOperationException("Could not pull the Mosquitto image", e); }
        var name = $"airport-{airportId.ToLowerInvariant()}";
        var prior = await client.Containers.ListContainersAsync(new ContainersListParameters { All = true, Filters = new Dictionary<string, IDictionary<string, bool>> { ["name"] = new Dictionary<string, bool> { [name] = true } } });
        var existing = prior.FirstOrDefault(x => x.Names.Contains("/" + name));
        if (existing != null)
        {
            if (existing.State != "running" && !await client.Containers.StartContainerAsync(existing.ID, new ContainerStartParameters()))
                throw new InvalidOperationException($"Could not restart broker for {airportId}");
            return;
        }
        var created = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = name,
            Image = Image,
            Cmd = ["mosquitto", "-c", $"/runtime/{confName}"],
            HostConfig = new HostConfig
            {
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                NetworkMode = Network,
                Mounts = [new Mount { Type = "volume", Source = Volume, Target = "/runtime", ReadOnly = true }]
            },
            NetworkingConfig = new NetworkingConfig { EndpointsConfig = new Dictionary<string, EndpointSettings> { [Network] = new() { Aliases = [$"airport-{airportId.ToLowerInvariant()}"] } } }
        });
        if (!await client.Containers.StartContainerAsync(created.ID, new ContainerStartParameters()))
            throw new InvalidOperationException($"Could not start broker for {airportId}");
        log.LogInformation("Started Mosquitto broker for {AirportId}", airportId);
    }
}
