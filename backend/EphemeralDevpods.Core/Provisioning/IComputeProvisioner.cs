using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Provisioning;

/// <summary>
/// Abstracts "run this environment spec somewhere." Implementations: LocalDockerProvisioner (dev),
/// AciProvisioner (production, Azure Container Instances).
/// </summary>
public interface IComputeProvisioner
{
    /// <summary>
    /// True when the environment's ports are published on the machine running it (local Docker), so the user
    /// picks which host port each one uses. Cloud providers give every environment its own address instead.
    /// </summary>
    bool PublishesHostPorts { get; }

    Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct);

    Task TeardownAsync(string environmentId, CancellationToken ct);

    /// <summary>Stops the environment's compute but keeps it so it can be started again (its tunnel is unregistered and set up again on start).</summary>
    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task StopAsync(string environmentId, CancellationToken ct);

    /// <summary>Starts a stopped environment; its entrypoint runs again, so it must be safe to re-run.</summary>
    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task StartAsync(string environmentId, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task RestartAsync(string environmentId, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct);
}
