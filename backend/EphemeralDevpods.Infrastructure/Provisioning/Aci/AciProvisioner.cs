using Azure;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerRegistry;
using Azure.ResourceManager.ContainerRegistry.Models;
using Azure.ResourceManager.Resources;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Infrastructure.Provisioning.Aci;

/// <summary>
/// Production provisioner: one Azure Container Instances container group per environment (docs/spec.md section 4).
/// Authenticates as a service principal via the injected <see cref="ArmClient"/>. Environments have no public IP; the
/// user reaches them through the VS Code tunnel.
/// </summary>
public sealed class AciProvisioner(ArmClient arm, AciOptions options) : IComputeProvisioner
{
    private static readonly TimeSpan BuildTimeout = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan BuildPollInterval = TimeSpan.FromSeconds(5);

    /// <summary>The tunnel's connect line can scroll out of a short tail if the dev server is chatty.</summary>
    private const int LogTailLines = 1000;

    public bool PublishesHostPorts => false;

    public async Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        var image = spec.DockerfilePath is null
            ? spec.Image!
            : await BuildImageAsync(environment, spec, ct);

        var tunnelName = EnvironmentNaming.TunnelNameFor(environment.EnvironmentId);
        var entrypointScript = EntrypointScript.Build(
            environment.RepoUrl, spec.PostCreateCommand, spec.PostAttachCommand, tunnelName,
            environment.TunnelProvider ?? TunnelProvider.GitHub, unregisterOnTerminate: true);

        var groupName = AciGroupBuilder.GroupName(environment.EnvironmentId);
        var groupData = AciGroupBuilder.Build(options, environment, spec, image, entrypointScript);

        ContainerGroupResource group;
        try
        {
            var operation = await ResourceGroup().GetContainerGroups()
                .CreateOrUpdateAsync(WaitUntil.Completed, groupName, groupData, ct);
            group = operation.Value;
        }
        catch
        {
            // A half-created group would keep billing with nothing pointing at it; never let cleanup mask the real error.
            await DeleteQuietlyAsync(environment.EnvironmentId);
            throw;
        }

        return new ProvisionResult
        {
            PublicUrl = "", // no public IP: dev servers are reached through the tunnel's port forwarding
            AccessToken = "",
            TunnelName = tunnelName,
            Resources =
            [
                new DeployedResource
                {
                    EnvironmentId = environment.EnvironmentId,
                    ResourceId = groupName,
                    Type = ResourceType.ContainerGroup,
                    ProviderResourceId = group.Id.ToString(),
                    Image = image,
                    Status = "Running",
                },
                new DeployedResource
                {
                    EnvironmentId = environment.EnvironmentId,
                    ResourceId = AciGroupBuilder.ContainerName,
                    Type = ResourceType.Container,
                    ProviderResourceId = $"{group.Id}/containers/{AciGroupBuilder.ContainerName}",
                    Image = image,
                    Status = "Running",
                },
            ],
        };
    }

    public async Task TeardownAsync(string environmentId, CancellationToken ct)
    {
        try
        {
            await Group(environmentId).DeleteAsync(WaitUntil.Completed, ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already gone (deleted earlier, or provisioning never got as far as creating it): teardown is idempotent.
        }
    }

    public async Task StopAsync(string environmentId, CancellationToken ct)
    {
        // ACI discards container state on stop, so the tunnel registration is released by the entrypoint's SIGTERM trap.
        var group = await GetGroupAsync(environmentId, ct);
        await group.StopAsync(ct);
    }

    public async Task StartAsync(string environmentId, CancellationToken ct)
    {
        var group = await GetGroupAsync(environmentId, ct);
        await group.StartAsync(WaitUntil.Started, ct); // a new deployment; status polling reports when it is running
    }

    public async Task RestartAsync(string environmentId, CancellationToken ct)
    {
        var group = await GetGroupAsync(environmentId, ct);
        await group.RestartAsync(WaitUntil.Started, ct);
    }

    public async Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct)
    {
        var group = await GetGroupAsync(environmentId, ct);
        return AciStatus.Map(group.Data.ProvisioningState, group.Data.InstanceView?.State);
    }

    public async Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct)
    {
        var group = await GetGroupAsync(environmentId, ct);

        string logs;
        try
        {
            logs = (await group.GetContainerLogsAsync(AciGroupBuilder.ContainerName, LogTailLines, timestamps: null, ct)).Value.Content
                ?? "";
        }
        catch (RequestFailedException ex) when (ex.Status is 400 or 404 or 409)
        {
            // The container isn't running yet (group still creating or transitioning), so there is nothing to read.
            return new TunnelState(TunnelPhase.Starting);
        }

        var fromLogs = TunnelLogParser.Parse(logs);
        if (fromLogs.Phase == TunnelPhase.AwaitingLogin)
        {
            return fromLogs;
        }

        // ACI exec is a single argument-less process behind a WebSocket, so `tunnel status` isn't an option here.
        return TunnelLogParser.IsConnectedInLogs(logs, EnvironmentNaming.TunnelNameFor(environmentId))
            ? new TunnelState(TunnelPhase.Ready)
            : fromLogs;
    }

    /// <exception cref="BuildContextNotFoundException">The Dockerfile isn't inside the build context.</exception>
    /// <exception cref="InvalidOperationException">The build failed, timed out, or pull credentials are missing.</exception>
    private async Task<string> BuildImageAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        // Fail before paying for a build whose image the container group then couldn't pull.
        if (!AciGroupBuilder.HasPullCredentials(options))
        {
            throw new InvalidOperationException(
                "Building images needs Compute:Aci:PullClientId and Compute:Aci:PullClientSecret so container groups can pull from the registry.");
        }

        var content = AciBuild.Content(environment.EnvironmentId, environment.RepoUrl, spec);
        var registry = arm.GetContainerRegistryResource(ContainerRegistryResource.CreateResourceIdentifier(
            options.SubscriptionId, options.RegistryResourceGroup, options.RegistryName));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(BuildTimeout);

        try
        {
            var operation = await registry.ScheduleRunAsync(WaitUntil.Completed, content, timeout.Token);
            var run = operation.Value;

            // Depending on how the service completes the long-running call, the run may still be queued or running.
            while (!AciBuild.IsTerminal(run.Data.Status))
            {
                await Task.Delay(BuildPollInterval, timeout.Token);
                run = (await run.GetAsync(timeout.Token)).Value;
            }

            if (run.Data.Status != ContainerRegistryRunStatus.Succeeded)
            {
                throw new InvalidOperationException(
                    $"ACR build {run.Data.RunId} ended {run.Data.Status}: {run.Data.RunErrorMessage}");
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"ACR build did not finish within {BuildTimeout.TotalMinutes:0} minutes.");
        }

        return AciBuild.ImageReference(options, environment.EnvironmentId);
    }

    /// <exception cref="KeyNotFoundException">No container group exists for this environment.</exception>
    private async Task<ContainerGroupResource> GetGroupAsync(string environmentId, CancellationToken ct)
    {
        try
        {
            return (await Group(environmentId).GetAsync(ct)).Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            throw new KeyNotFoundException($"No container group found for environment {environmentId}.", ex);
        }
    }

    private async Task DeleteQuietlyAsync(string environmentId)
    {
        try
        {
            await Group(environmentId).DeleteAsync(WaitUntil.Completed);
        }
        catch (RequestFailedException)
        {
            // Best effort: the caller is already failing with the real error, and the TTL sweep is the backstop.
        }
    }

    private ResourceGroupResource ResourceGroup() =>
        arm.GetResourceGroupResource(ResourceGroupResource.CreateResourceIdentifier(options.SubscriptionId, options.ResourceGroup));

    private ContainerGroupResource Group(string environmentId) =>
        arm.GetContainerGroupResource(ContainerGroupResource.CreateResourceIdentifier(
            options.SubscriptionId, options.ResourceGroup, AciGroupBuilder.GroupName(environmentId)));
}
