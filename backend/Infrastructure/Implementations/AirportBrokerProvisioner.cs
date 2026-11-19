using System.Net;
using Docker.DotNet;
using Docker.DotNet.Models;
using Microsoft.Extensions.Configuration;

namespace Luggage;

public sealed class AirportBrokerProvisioner(
    IConfiguration config) : IAirportBrokerProvisioner
{
    private const string ImageName = "eclipse-mosquitto";
    private const string ImageTag = "2.0.22";
    private const string Image = $"{ImageName}:{ImageTag}";
    private const string Network = "airport-brokers";
    private const string Volume = "airport-broker-config";

    public async Task Provision(string airportId)
    {
        var (confName, aclName) = await WriteBrokerFilesAsync(airportId);

        using var client = CreateDockerClient();
        await EnsureNetworkAsync(client);
        await EnsureImageAsync(client);

        var containerName = $"airport-{airportId.ToLowerInvariant()}";
        var existing = await FindExistingContainerAsync(client, containerName);

        if (existing is not null)
        {
            if (existing.State != "running")
            {
                await StartContainerAsync(client, existing.ID, airportId, "Restarted");
            }

            return;
        }

        var created = await client.Containers.CreateContainerAsync(new CreateContainerParameters
        {
            Name = containerName,
            Image = Image,
            Cmd = ["mosquitto", "-c", $"/runtime/{confName}"],
            HostConfig = new HostConfig
            {
                RestartPolicy = new RestartPolicy { Name = RestartPolicyKind.UnlessStopped },
                NetworkMode = Network,
                Mounts =
                [
                    new Mount
                    {
                        Type = "volume",
                        Source = Volume,
                        Target = "/runtime",
                        ReadOnly = true
                    }
                ]
            },
            NetworkingConfig = new NetworkingConfig
            {
                EndpointsConfig = new Dictionary<string, EndpointSettings>
                {
                    [Network] = new EndpointSettings { Aliases = [containerName] }
                }
            }
        });

        await StartContainerAsync(client, created.ID, airportId, "Started");
    }

    private async Task<(string ConfName, string AclName)> WriteBrokerFilesAsync(string airportId)
    {
        var root = config["Brokers:ConfigPath"] ?? "/broker-config";
        Directory.CreateDirectory(root);

        var confName = $"{airportId}.conf";
        var aclName = $"{airportId}.acl";
        var acl = $"topic read airport/{airportId}/#\ntopic write airport/{airportId}/#\n";
        var conf = $"""
            listener 1883 0.0.0.0
            allow_anonymous true
            acl_file /runtime/{aclName}
            connection regional-{airportId}
            address regional:1883
            local_clientid bridge-{airportId}
            remote_clientid bridge-{airportId}
            try_private false
            notifications false
            cleansession false
            restart_timeout 1 30
            topic airport/{airportId}/# out 1
            """;

        await File.WriteAllTextAsync(Path.Combine(root, aclName), acl);
        await File.WriteAllTextAsync(Path.Combine(root, confName), conf);

        return (confName, aclName);
    }

    private IDockerClient CreateDockerClient()
    {
        var endpoint = config["Docker:Endpoint"] ?? "unix:///var/run/docker.sock";
        return new DockerClientConfiguration(new Uri(endpoint)).CreateClient();
    }

    private static async Task EnsureNetworkAsync(IDockerClient client)
    {
        try
        {
            await client.Networks.CreateNetworkAsync(new NetworksCreateParameters
            {
                Name = Network,
                CheckDuplicate = true,
                Driver = "bridge"
            });
        }
        catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            // Network already exists.
        }
    }

    private static async Task EnsureImageAsync(IDockerClient client)
    {
        try
        {
            await client.Images.InspectImageAsync(Image);
            return;
        }
        catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            // Image not present locally; pull below.
        }

        try
        {
            await client.Images.CreateImageAsync(
                new ImagesCreateParameters { FromImage = ImageName, Tag = ImageTag }, null, new Progress<JSONMessage>());
        }
        catch (DockerApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Could not pull {Image}", ex);
        }
    }

    private static async Task<ContainerListResponse?> FindExistingContainerAsync(IDockerClient client, string containerName)
    {
        var containers = await client.Containers.ListContainersAsync(new ContainersListParameters
        {
            All = true,
            Filters = new Dictionary<string, IDictionary<string, bool>>
            {
                ["name"] = new Dictionary<string, bool> { [containerName] = true }
            }
        });

        return containers.FirstOrDefault(c => c.Names.Any(n => n.TrimStart('/') == containerName));
    }

    private async Task StartContainerAsync(IDockerClient client, string containerId, string airportId, string action)
    {
        if (!await client.Containers.StartContainerAsync(containerId, new ContainerStartParameters()))
        {
            throw new InvalidOperationException($"Could not {action.ToLowerInvariant()} broker for {airportId}");
        }

    }
}
