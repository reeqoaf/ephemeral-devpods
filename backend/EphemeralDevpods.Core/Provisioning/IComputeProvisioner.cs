using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Provisioning;

/// <summary>
/// Abstracts "run this environment spec somewhere." Implementations: LocalDockerProvisioner (dev),
/// AciProvisioner (production, Azure Container Instances).
/// </summary>
public interface IComputeProvisioner
{
    Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct);

    Task TeardownAsync(string environmentId, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct);

    /// <exception cref="KeyNotFoundException">No compute resource exists for this environment.</exception>
    Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct);
}
