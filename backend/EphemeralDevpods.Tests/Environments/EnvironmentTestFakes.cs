using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Tests.Environments;

internal sealed class FakeProvisioner : IComputeProvisioner
{
    public bool PublishesHostPorts { get; set; } = true;
    public EnvironmentStatus Status { get; set; } = EnvironmentStatus.Running;
    public bool ContainerGone { get; set; }
    public TunnelState Tunnel { get; set; } = new(TunnelPhase.Starting);
    public int TunnelLookups { get; private set; }

    /// <summary>Thrown by stop/start/restart when set, to test that failed actions leave state untouched.</summary>
    public Exception? LifecycleFailure { get; set; }

    /// <summary>Lifecycle calls in order, as "stop" / "start" / "restart".</summary>
    public List<string> Calls { get; } = [];

    public Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task TeardownAsync(string environmentId, CancellationToken ct) => throw new NotSupportedException();

    public Task StopAsync(string environmentId, CancellationToken ct) => Lifecycle("stop");

    public Task StartAsync(string environmentId, CancellationToken ct) => Lifecycle("start");

    public Task RestartAsync(string environmentId, CancellationToken ct) => Lifecycle("restart");

    public Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct) =>
        ContainerGone ? throw new KeyNotFoundException() : Task.FromResult(Status);

    public Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct)
    {
        TunnelLookups++;
        return Task.FromResult(Tunnel);
    }

    private Task Lifecycle(string call)
    {
        Calls.Add(call);
        return LifecycleFailure is null ? Task.CompletedTask : throw LifecycleFailure;
    }
}

internal sealed class FakeRepository : IEnvironmentRepository
{
    public int Upserts { get; private set; }

    /// <summary>What <see cref="ListActiveAsync"/> returns.</summary>
    public List<WorkspaceEnvironment> Active { get; } = [];

    public Task<WorkspaceEnvironment?> GetAsync(string owner, string environmentId, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkspaceEnvironment>> ListByOwnerAsync(string owner, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkspaceEnvironment>> ListExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkspaceEnvironment>> ListActiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkspaceEnvironment>>(Active);

    public Task UpsertAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        Upserts++;
        return Task.CompletedTask;
    }
}

internal static class TestEnvironments
{
    public static WorkspaceEnvironment Make(
        EnvironmentStatus status = EnvironmentStatus.Running, bool tunnelReady = false) => new()
    {
        Owner = "owner",
        EnvironmentId = Guid.NewGuid().ToString(),
        RepoUrl = "https://github.com/owner/repo",
        Status = status,
        TtlMinutes = 60,
        CreatedAt = DateTimeOffset.UtcNow,
        TunnelName = "epd-12345678",
        TunnelReady = tunnelReady,
    };
}
