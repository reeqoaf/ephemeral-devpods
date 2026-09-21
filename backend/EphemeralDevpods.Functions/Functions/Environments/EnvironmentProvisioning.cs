using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>
/// Runs a queued <see cref="ProvisionRequest"/>: provisions the compute and moves the environment row from
/// Provisioning to Running (or Failed). Safe to run twice for the same message — it only acts on a row that is
/// still Provisioning.
/// </summary>
public sealed class EnvironmentProvisioning(
    IComputeProvisioner provisioner, IEnvironmentRepository environments, IResourceRepository resources,
    ILogger<EnvironmentProvisioning> logger)
{
    public async Task RunAsync(ProvisionRequest request, CancellationToken ct)
    {
        var environment = await environments.GetAsync(request.Owner, request.EnvironmentId, ct);
        if (environment is not { Status: EnvironmentStatus.Provisioning })
        {
            return; // deleted or expired while queued, or an earlier delivery already finished it
        }

        ProvisionResult result;
        try
        {
            result = await provisioner.ProvisionAsync(environment, request.Spec, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            // A host shutdown isn't a failed provision (the message is retried); anything else is recorded on the row.
            logger.LogError(ex, "Provisioning failed for environment {EnvironmentId}", request.EnvironmentId);
            await MarkFailedAsync(request, ct);
            return;
        }

        // The user may have deleted the environment while it was being built; don't resurrect it, remove what was just made.
        var current = await environments.GetAsync(request.Owner, request.EnvironmentId, ct);
        if (current is not { Status: EnvironmentStatus.Provisioning })
        {
            await provisioner.TeardownAsync(request.EnvironmentId, ct);
            return;
        }

        foreach (var resource in result.Resources)
        {
            await resources.UpsertAsync(resource, ct);
        }

        current.Status = EnvironmentStatus.Running;
        current.PublicUrl = result.PublicUrl;
        current.AccessToken = result.AccessToken;
        current.TunnelName = result.TunnelName;
        await environments.UpsertAsync(current, ct);
    }

    private async Task MarkFailedAsync(ProvisionRequest request, CancellationToken ct)
    {
        var current = await environments.GetAsync(request.Owner, request.EnvironmentId, ct);
        if (current is { Status: EnvironmentStatus.Provisioning })
        {
            current.Status = EnvironmentStatus.Failed;
            await environments.UpsertAsync(current, ct);
        }
    }
}
