using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>
/// Stop / start / restart of an existing environment's container. Each action checks the status it is
/// allowed from (otherwise <see cref="InvalidEnvironmentStateException"/>, a 409), asks the provisioner,
/// and only then records the new status — so a provisioner failure leaves the stored state untouched.
/// Expiry is unaffected: it is always measured from creation, whether the environment is running or stopped.
/// </summary>
public sealed class EnvironmentLifecycle(IComputeProvisioner provisioner, IEnvironmentRepository environments)
{
    public async Task StopAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        RequireStatus(environment, "stop", EnvironmentStatus.Running, EnvironmentStatus.Provisioning);

        await provisioner.StopAsync(environment.EnvironmentId, ct);
        environment.Status = EnvironmentStatus.Stopped;
        await environments.UpsertAsync(environment, ct);
    }

    /// <summary>Also allowed from Failed: a crashed container still exists and can simply be started again.</summary>
    public async Task StartAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        RequireStatus(environment, "start", EnvironmentStatus.Stopped, EnvironmentStatus.Failed);

        await provisioner.StartAsync(environment.EnvironmentId, ct);
        environment.Status = EnvironmentStatus.Running;
        environment.TunnelReady = false; // the tunnel has to reconnect (and may need a new login)
        await environments.UpsertAsync(environment, ct);
    }

    public async Task RestartAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        RequireStatus(environment, "restart", EnvironmentStatus.Running);

        await provisioner.RestartAsync(environment.EnvironmentId, ct);
        environment.TunnelReady = false;
        await environments.UpsertAsync(environment, ct);
    }

    private static void RequireStatus(
        WorkspaceEnvironment environment, string action, params EnvironmentStatus[] allowed)
    {
        if (!allowed.Contains(environment.Status))
        {
            throw new InvalidEnvironmentStateException(action, environment.Status);
        }
    }
}
