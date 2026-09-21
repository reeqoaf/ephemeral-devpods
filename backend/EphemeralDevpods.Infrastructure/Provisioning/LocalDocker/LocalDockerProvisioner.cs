using Docker.DotNet;
using Docker.DotNet.Models;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;

/// <summary>
/// Local-dev stand-in for AciProvisioner: runs the environment as a real Docker container via
/// Docker.DotNet (docs/spec.md §13). One container per environment, ports published on
/// localhost, no access token needed since nothing leaves the machine.
/// </summary>
public sealed class LocalDockerProvisioner(IDockerClient dockerClient, IBuildContextFetcher buildContextFetcher)
    : IComputeProvisioner
{
    private const string EnvironmentIdLabel = "ephemeral-devpods.environmentId";

    public bool PublishesHostPorts => true;

    public async Task<ProvisionResult> ProvisionAsync(
        WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        var imageRef = await ResolveImageAsync(environment, spec, ct);
        var tunnelName = EnvironmentNaming.TunnelNameFor(environment.EnvironmentId);
        var entrypointScript = EntrypointScript.Build(
            environment.RepoUrl, spec.PostCreateCommand, spec.PostAttachCommand, tunnelName,
            environment.TunnelProvider ?? TunnelProvider.GitHub);

        // Environments created before ports were selectable published each port at the same number on the host.
        var portMappings = environment.PortMappings ?? PortMapping.Identity(spec.ForwardPorts);

        var exposedPorts = new Dictionary<string, EmptyStruct>();
        var portBindings = new Dictionary<string, IList<PortBinding>>();
        foreach (var mapping in portMappings)
        {
            var key = $"{mapping.ContainerPort}/tcp";
            exposedPorts[key] = new EmptyStruct();
            portBindings[key] = [new PortBinding { HostPort = mapping.HostPort.ToString() }];
        }

        var createResponse = await dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Name = $"ephemeral-devpods-{environment.EnvironmentId}",
                Image = imageRef,
                Env = spec.ContainerEnv.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
                Entrypoint = ["sh", "-c"],
                Cmd = [entrypointScript],
                ExposedPorts = exposedPorts,
                Labels = new Dictionary<string, string> { [EnvironmentIdLabel] = environment.EnvironmentId },
                HostConfig = new HostConfig
                {
                    PortBindings = portBindings,
                    Memory = environment.MemoryMb is { } memoryMb ? memoryMb * 1024L * 1024L : 0,
                    NanoCPUs = environment.CpuCores is { } cpuCores ? cpuCores * 1_000_000_000L : 0,
                },
            },
            ct);

        await dockerClient.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters(), ct);

        var resource = new DeployedResource
        {
            EnvironmentId = environment.EnvironmentId,
            ResourceId = createResponse.ID,
            Type = ResourceType.Container,
            ProviderResourceId = createResponse.ID,
            Image = imageRef,
            Port = portMappings.Count > 0 ? portMappings[0].ContainerPort : null,
            Status = "Running",
        };

        return new ProvisionResult
        {
            PublicUrl = portMappings.Count > 0 ? $"http://localhost:{portMappings[0].HostPort}" : "",
            AccessToken = "", // purely localhost-bound — no token needed, nothing is internet-reachable
            TunnelName = tunnelName,
            Resources = [resource],
        };
    }

    public async Task TeardownAsync(string environmentId, CancellationToken ct)
    {
        foreach (var container in await FindContainersAsync(environmentId, ct))
        {
            await UnregisterTunnelAsync(container, ct);
            await dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters(), ct);
            await dockerClient.Containers.RemoveContainerAsync(
                container.ID, new ContainerRemoveParameters { Force = true }, ct);
        }
    }

    public async Task StopAsync(string environmentId, CancellationToken ct)
    {
        var container = await FindContainerAsync(environmentId, ct);

        // A stopped container can't run `tunnel unregister`, so a Delete or TTL sweep of a stopped environment
        // would leak its tunnel against the user's account limit. Start registers it again.
        await UnregisterTunnelAsync(container, ct);
        await dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters(), ct);
    }

    public async Task StartAsync(string environmentId, CancellationToken ct)
    {
        var container = await FindContainerAsync(environmentId, ct);
        await dockerClient.Containers.StartContainerAsync(container.ID, new ContainerStartParameters(), ct);
    }

    public async Task RestartAsync(string environmentId, CancellationToken ct)
    {
        var container = await FindContainerAsync(environmentId, ct);
        await dockerClient.Containers.RestartContainerAsync(container.ID, new ContainerRestartParameters(), ct);
    }

    public async Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct)
    {
        var container = await FindContainerAsync(environmentId, ct);

        return container.State switch
        {
            "running" => EnvironmentStatus.Running,
            "created" or "restarting" => EnvironmentStatus.Provisioning,
            _ => EnvironmentStatus.Failed,
        };
    }

    public async Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct)
    {
        var container = await FindContainerAsync(environmentId, ct);

        using var logs = await dockerClient.Containers.GetContainerLogsAsync(
            container.ID,
            tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Tail = "200" },
            ct);
        var (stdout, stderr) = await logs.ReadOutputToEndAsync(ct);

        var fromLogs = TunnelLogParser.Parse(stdout + stderr);
        if (fromLogs.Phase == TunnelPhase.AwaitingLogin)
        {
            return fromLogs;
        }

        var status = await ExecAsync(container.ID, [EntrypointScript.CodeCliPath, "tunnel", "status"], ct);
        return status is not null && TunnelLogParser.IsConnected(status)
            ? new TunnelState(TunnelPhase.Ready)
            : fromLogs;
    }

    /// <summary>
    /// Best effort: a tunnel left registered keeps counting against the user's per-account tunnel limit
    /// after its container is gone. Teardown must never fail because of this.
    /// </summary>
    private async Task UnregisterTunnelAsync(ContainerListResponse container, CancellationToken ct)
    {
        if (container.State != "running")
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await ExecAsync(container.ID, [EntrypointScript.CodeCliPath, "tunnel", "unregister"], timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // unregister hung (e.g. waiting on a login) — carry on with the teardown
        }
    }

    /// <summary>Runs a command in the container; null if it couldn't be started (container stopping, CLI missing).</summary>
    private async Task<string?> ExecAsync(string containerId, string[] command, CancellationToken ct)
    {
        try
        {
            var exec = await dockerClient.Exec.ExecCreateContainerAsync(
                containerId,
                new ContainerExecCreateParameters { AttachStdout = true, AttachStderr = true, Cmd = command },
                ct);
            using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(exec.ID, tty: false, ct);
            var (stdout, _) = await stream.ReadOutputToEndAsync(ct);
            return stdout;
        }
        catch (DockerApiException)
        {
            return null;
        }
    }

    /// <exception cref="KeyNotFoundException">No container exists for this environment.</exception>
    private async Task<ContainerListResponse> FindContainerAsync(string environmentId, CancellationToken ct) =>
        (await FindContainersAsync(environmentId, ct)).FirstOrDefault()
        ?? throw new KeyNotFoundException($"No local Docker container found for environment {environmentId}.");

    private Task<IList<ContainerListResponse>> FindContainersAsync(string environmentId, CancellationToken ct) =>
        dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"{EnvironmentIdLabel}={environmentId}"] = true },
                },
            },
            ct);

    private async Task<string> ResolveImageAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        if (spec.DockerfilePath is null)
        {
            await PullImageAsync(spec.Image!, ct);
            return spec.Image!;
        }

        var buildContext = await buildContextFetcher.FetchAsync(
            environment.RepoUrl, spec.DevcontainerBaseDirectory, spec.BuildContextPath ?? ".", spec.DockerfilePath, ct);

        await using (buildContext.Tar)
        {
            var tag = $"ephemeral-devpods/{environment.EnvironmentId}:latest";
            string? buildError = null;

            var progress = new Progress<JSONMessage>(message =>
            {
                if (!string.IsNullOrEmpty(message.ErrorMessage))
                {
                    buildError = message.ErrorMessage;
                }
            });

            await dockerClient.Images.BuildImageFromDockerfileAsync(
                new ImageBuildParameters
                {
                    Dockerfile = buildContext.DockerfilePathInContext,
                    Tags = [tag],
                    BuildArgs = spec.BuildArgs.Count > 0 ? new Dictionary<string, string>(spec.BuildArgs) : null,
                },
                buildContext.Tar,
                authConfigs: [],
                headers: new Dictionary<string, string>(),
                progress,
                ct);

            if (buildError is not null)
            {
                throw new InvalidOperationException($"Docker build failed: {buildError}");
            }

            return tag;
        }
    }

    private async Task PullImageAsync(string image, CancellationToken ct)
    {
        var (fromImage, tag) = SplitImageTag(image);
        await dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
            authConfig: null,
            new Progress<JSONMessage>(),
            ct);
    }

    /// <summary>Splits "repo:tag" — careful not to mistake a registry port for a tag separator.</summary>
    private static (string FromImage, string Tag) SplitImageTag(string image)
    {
        var lastColon = image.LastIndexOf(':');
        return lastColon > image.LastIndexOf('/')
            ? (image[..lastColon], image[(lastColon + 1)..])
            : (image, "latest");
    }
}
