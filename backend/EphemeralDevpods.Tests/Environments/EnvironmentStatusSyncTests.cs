using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class EnvironmentStatusSyncTests
{
    [Fact]
    public async Task Latches_tunnel_ready_and_persists_it_once()
    {
        var provisioner = new FakeProvisioner { Tunnel = new TunnelState(TunnelPhase.Ready) };
        var repository = new FakeRepository();
        var sync = new EnvironmentStatusSync(provisioner, repository);
        var environment = TestEnvironments.Make();

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

        var snapshot = await sync.RefreshAsync(TestEnvironments.Make(), CancellationToken.None);

        Assert.Equal("AAAA-1111", snapshot.Tunnel?.DeviceCode);
        Assert.False(snapshot.Environment.TunnelReady);
        Assert.Equal(0, repository.Upserts); // a device code is short-lived and never stored
    }

    [Fact]
    public async Task Skips_the_tunnel_lookup_when_already_ready()
    {
        var provisioner = new FakeProvisioner();
        var sync = new EnvironmentStatusSync(provisioner, new FakeRepository());

        var snapshot = await sync.RefreshAsync(TestEnvironments.Make(tunnelReady: true), CancellationToken.None);

        Assert.Null(snapshot.Tunnel);
        Assert.Equal(0, provisioner.TunnelLookups);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Provisioning)]
    [InlineData(EnvironmentStatus.Expired)]
    [InlineData(EnvironmentStatus.Failed)]
    [InlineData(EnvironmentStatus.Stopped)]
    public async Task Skips_the_tunnel_lookup_unless_the_environment_is_running(EnvironmentStatus status)
    {
        var provisioner = new FakeProvisioner { Status = status };
        var sync = new EnvironmentStatusSync(provisioner, new FakeRepository());

        var snapshot = await sync.RefreshAsync(TestEnvironments.Make(status), CancellationToken.None);

        Assert.Null(snapshot.Tunnel);
        Assert.Equal(0, provisioner.TunnelLookups);
    }

    [Fact]
    public async Task A_vanished_container_becomes_failed_without_a_tunnel_lookup()
    {
        var provisioner = new FakeProvisioner { ContainerGone = true };
        var repository = new FakeRepository();
        var sync = new EnvironmentStatusSync(provisioner, repository);

        var snapshot = await sync.RefreshAsync(TestEnvironments.Make(), CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Failed, snapshot.Environment.Status);
        Assert.Equal(0, provisioner.TunnelLookups);
        Assert.Equal(1, repository.Upserts);
    }
}
