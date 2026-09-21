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
    public async Task<EnvironmentSnapshot> RefreshAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        if (environment.Status is not (EnvironmentStatus.Running or EnvironmentStatus.Provisioning))
        {
            return new EnvironmentSnapshot(environment, null); // Expired/Failed/Stopped: nothing live to check (Stopped is deliberate)
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

        var changed = liveStatus != environment.Status;
        environment.Status = liveStatus;

        // Once the tunnel has connected it's latched on the row, so only environments still waiting
        // on the user's device-code login pay for a log read on each refresh.
        TunnelState? tunnel = null;
        if (environment.Status == EnvironmentStatus.Running && !environment.TunnelReady)
        {
            try
            {
                tunnel = await provisioner.GetTunnelStateAsync(environment.EnvironmentId, ct);
            }
            catch (KeyNotFoundException)
            {
                // Container vanished between the status check and now; the next refresh marks it Failed.
            }

            if (tunnel?.Phase == TunnelPhase.Ready)
            {
                environment.TunnelReady = true;
                changed = true;
            }
        }

        if (changed)
        {
            await environments.UpsertAsync(environment, ct);
        }

        return new EnvironmentSnapshot(environment, tunnel);
    }
}

/// <summary>An environment plus its live (never persisted) tunnel state, as of this refresh.</summary>
public sealed record EnvironmentSnapshot(WorkspaceEnvironment Environment, TunnelState? Tunnel);
