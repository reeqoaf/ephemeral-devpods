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

    /// <summary>What <see cref="ProvisionAsync"/> returns.</summary>
    public ProvisionResult ProvisionOutcome { get; set; } = new()
    {
        PublicUrl = "http://localhost:3000",
        AccessToken = "",
        TunnelName = "epd-12345678",
        Resources = [],
    };

    /// <summary>Thrown by <see cref="ProvisionAsync"/> when set.</summary>
    public Exception? ProvisionFailure { get; set; }

    /// <summary>Runs inside <see cref="ProvisionAsync"/> before it returns, to simulate things happening mid-provision.</summary>
    public Func<Task>? DuringProvision { get; set; }

    /// <summary>Environment ids passed to <see cref="TeardownAsync"/>.</summary>
    public List<string> TornDown { get; } = [];

    public async Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        if (DuringProvision is not null)
        {
            await DuringProvision();
        }

        return ProvisionFailure is null ? ProvisionOutcome : throw ProvisionFailure;
    }

    public Task TeardownAsync(string environmentId, CancellationToken ct)
    {
        TornDown.Add(environmentId);
        return Task.CompletedTask;
    }

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

    /// <summary>Rows by environment id, for tests that read and write single environments.</summary>
    public Dictionary<string, WorkspaceEnvironment> Rows { get; } = [];

    public Task<WorkspaceEnvironment?> GetAsync(string owner, string environmentId, CancellationToken ct) =>
        Task.FromResult(Rows.GetValueOrDefault(environmentId) is { } row && row.Owner == owner ? row : null);

    public Task<IReadOnlyList<WorkspaceEnvironment>> ListByOwnerAsync(string owner, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkspaceEnvironment>> ListExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
        throw new NotSupportedException();

    public Task<IReadOnlyList<WorkspaceEnvironment>> ListActiveAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<WorkspaceEnvironment>>(Active);

    public Task UpsertAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        Upserts++;
        Rows[environment.EnvironmentId] = environment;
        return Task.CompletedTask;
    }
}

internal sealed class FakeResourceRepository : IResourceRepository
{
    public List<DeployedResource> Stored { get; } = [];

    public Task<IReadOnlyList<DeployedResource>> ListByEnvironmentAsync(string environmentId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<DeployedResource>>(Stored.Where(r => r.EnvironmentId == environmentId).ToList());

    public Task UpsertAsync(DeployedResource resource, CancellationToken ct)
    {
        Stored.Add(resource);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string environmentId, string resourceId, CancellationToken ct) =>
        throw new NotSupportedException();
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
