using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>
/// Table Storage only ever reflects the state as of the last provision/refresh, not truly live
/// Docker/ACI state — nothing re-polls it in the background. ListEnvironments and GetEnvironment
/// call this before responding so the dashboard reflects reality (e.g. a container that has
/// since crashed or exited) instead of a stale "Running" snapshot from provision time.
/// </summary>
public sealed class EnvironmentStatusSync(IComputeProvisioner provisioner, IEnvironmentRepository environments)
{
    public async Task<WorkspaceEnvironment> RefreshAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        if (environment.Status is not (EnvironmentStatus.Running or EnvironmentStatus.Provisioning))
        {
            return environment; // Expired/Failed are terminal — nothing left to check
        }

        EnvironmentStatus liveStatus;
        try
        {
            liveStatus = await provisioner.GetStatusAsync(environment.EnvironmentId, ct);
        }
        catch (KeyNotFoundException)
        {
            // The container is gone (crashed and got reaped, removed outside our flow, etc.) —
            // that's not a status we were expecting, so surface it rather than keep claiming Running.
            liveStatus = EnvironmentStatus.Failed;
        }

        if (liveStatus != environment.Status)
        {
            environment.Status = liveStatus;
            await environments.UpsertAsync(environment, ct);
        }

        return environment;
    }
}
