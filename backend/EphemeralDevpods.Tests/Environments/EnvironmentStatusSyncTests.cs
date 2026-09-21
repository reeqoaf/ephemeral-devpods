using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class EnvironmentStatusSyncTests
{
    private sealed class FakeProvisioner : IComputeProvisioner
    {
        public EnvironmentStatus Status { get; set; } = EnvironmentStatus.Running;
        public bool ContainerGone { get; set; }
        public TunnelState Tunnel { get; set; } = new(TunnelPhase.Starting);
        public int TunnelLookups { get; private set; }

        public Task<ProvisionResult> ProvisionAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task TeardownAsync(string environmentId, CancellationToken ct) => throw new NotSupportedException();

        public Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct) =>
            ContainerGone ? throw new KeyNotFoundException() : Task.FromResult(Status);

        public Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct)
        {
            TunnelLookups++;
            return Task.FromResult(Tunnel);
        }
    }

    private sealed class FakeRepository : IEnvironmentRepository
    {
        public int Upserts { get; private set; }

        public Task<WorkspaceEnvironment?> GetAsync(string owner, string environmentId, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WorkspaceEnvironment>> ListByOwnerAsync(string owner, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<WorkspaceEnvironment>> ListExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken ct) =>
            throw new NotSupportedException();

        public Task UpsertAsync(WorkspaceEnvironment environment, CancellationToken ct)
        {
            Upserts++;
            return Task.CompletedTask;
        }
    }

    private static WorkspaceEnvironment MakeEnvironment(
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

    [Fact]
    public async Task Latches_tunnel_ready_and_persists_it_once()
    {
        var provisioner = new FakeProvisioner { Tunnel = new TunnelState(TunnelPhase.Ready) };
        var repository = new FakeRepository();
        var sync = new EnvironmentStatusSync(provisioner, repository);
        var environment = MakeEnvironment();

        var first = await sync.RefreshAsync(environment, CancellationToken.None);
        await sync.RefreshAsync(environment, CancellationToken.None);

        Assert.True(first.Environment.TunnelReady);
        Assert.Equal(TunnelPhase.Ready, first.Tunnel?.Phase);
        Assert.Equal(1, repository.Upserts);
        Assert.Equal(1, provisioner.TunnelLookups); // the second refresh skipped the lookup
    }

    [Fact]
    public async Task Awaiting_login_is_returned_but_not_persisted()
    {
        var provisioner = new FakeProvisioner { Tunnel = new TunnelState(TunnelPhase.AwaitingLogin, "AAAA-1111", "https://github.com/login/device") };
        var repository = new FakeRepository();
        var sync = new EnvironmentStatusSync(provisioner, repository);

        var snapshot = await sync.RefreshAsync(MakeEnvironment(), CancellationToken.None);

        Assert.Equal("AAAA-1111", snapshot.Tunnel?.DeviceCode);
        Assert.False(snapshot.Environment.TunnelReady);
        Assert.Equal(0, repository.Upserts); // a device code is short-lived and never stored
    }

    [Fact]
    public async Task Skips_the_tunnel_lookup_when_already_ready()
    {
        var provisioner = new FakeProvisioner();
        var sync = new EnvironmentStatusSync(provisioner, new FakeRepository());

        var snapshot = await sync.RefreshAsync(MakeEnvironment(tunnelReady: true), CancellationToken.None);

        Assert.Null(snapshot.Tunnel);
        Assert.Equal(0, provisioner.TunnelLookups);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Provisioning)]
    [InlineData(EnvironmentStatus.Expired)]
    [InlineData(EnvironmentStatus.Failed)]
    public async Task Skips_the_tunnel_lookup_unless_the_environment_is_running(EnvironmentStatus status)
    {
        var provisioner = new FakeProvisioner { Status = status };
        var sync = new EnvironmentStatusSync(provisioner, new FakeRepository());

        var snapshot = await sync.RefreshAsync(MakeEnvironment(status), CancellationToken.None);

        Assert.Null(snapshot.Tunnel);
        Assert.Equal(0, provisioner.TunnelLookups);
    }

    [Fact]
    public async Task A_vanished_container_becomes_failed_without_a_tunnel_lookup()
    {
        var provisioner = new FakeProvisioner { ContainerGone = true };
        var repository = new FakeRepository();
        var sync = new EnvironmentStatusSync(provisioner, repository);

        var snapshot = await sync.RefreshAsync(MakeEnvironment(), CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Failed, snapshot.Environment.Status);
        Assert.Equal(0, provisioner.TunnelLookups);
        Assert.Equal(1, repository.Upserts);
    }
}
