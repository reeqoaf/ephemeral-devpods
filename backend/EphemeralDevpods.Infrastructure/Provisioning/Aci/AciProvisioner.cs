using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Infrastructure.Provisioning.Aci;

/// <summary>Production provisioner: deploys one Azure Container Instances container group per environment.</summary>
public sealed class AciProvisioner : IComputeProvisioner
{
    public bool PublishesHostPorts => false;

    public Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task TeardownAsync(string environmentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(string environmentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task StartAsync(string environmentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task RestartAsync(string environmentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }

    public Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct)
    {
        throw new NotImplementedException();
    }
}
