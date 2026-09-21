using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class EnvironmentLifecycleTests
{
    private readonly FakeProvisioner _provisioner = new();
    private readonly FakeRepository _repository = new();
    private readonly EnvironmentLifecycle _lifecycle;

    public EnvironmentLifecycleTests()
    {
        _lifecycle = new EnvironmentLifecycle(_provisioner, _repository);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Running)]
    [InlineData(EnvironmentStatus.Provisioning)]
    public async Task Stop_marks_the_environment_stopped(EnvironmentStatus from)
    {
        var environment = TestEnvironments.Make(from);

        await _lifecycle.StopAsync(environment, CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Stopped, environment.Status);
        Assert.Equal(["stop"], _provisioner.Calls);
        Assert.Equal(1, _repository.Upserts);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Stopped)]
    [InlineData(EnvironmentStatus.Failed)]
    public async Task Start_marks_the_environment_running_and_resets_the_tunnel(EnvironmentStatus from)
    {
        var environment = TestEnvironments.Make(from, tunnelReady: true);

        await _lifecycle.StartAsync(environment, CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Running, environment.Status);
        Assert.False(environment.TunnelReady);
        Assert.Equal(["start"], _provisioner.Calls);
        Assert.Equal(1, _repository.Upserts);
    }

    [Fact]
    public async Task Restart_keeps_the_environment_running_and_resets_the_tunnel()
    {
        var environment = TestEnvironments.Make(EnvironmentStatus.Running, tunnelReady: true);

        await _lifecycle.RestartAsync(environment, CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Running, environment.Status);
        Assert.False(environment.TunnelReady);
        Assert.Equal(["restart"], _provisioner.Calls);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Stopped)]
    [InlineData(EnvironmentStatus.Expired)]
    [InlineData(EnvironmentStatus.Failed)]
    public async Task Stop_is_rejected_unless_the_environment_is_live(EnvironmentStatus from)
    {
        await Assert.ThrowsAsync<InvalidEnvironmentStateException>(
            () => _lifecycle.StopAsync(TestEnvironments.Make(from), CancellationToken.None));

        Assert.Empty(_provisioner.Calls);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Running)]
    [InlineData(EnvironmentStatus.Provisioning)]
    [InlineData(EnvironmentStatus.Expired)]
    public async Task Start_is_rejected_unless_the_environment_is_stopped_or_failed(EnvironmentStatus from)
    {
        await Assert.ThrowsAsync<InvalidEnvironmentStateException>(
            () => _lifecycle.StartAsync(TestEnvironments.Make(from), CancellationToken.None));

        Assert.Empty(_provisioner.Calls);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Stopped)]
    [InlineData(EnvironmentStatus.Provisioning)]
    [InlineData(EnvironmentStatus.Failed)]
    [InlineData(EnvironmentStatus.Expired)]
    public async Task Restart_is_rejected_unless_the_environment_is_running(EnvironmentStatus from)
    {
        await Assert.ThrowsAsync<InvalidEnvironmentStateException>(
            () => _lifecycle.RestartAsync(TestEnvironments.Make(from), CancellationToken.None));

        Assert.Empty(_provisioner.Calls);
    }

    [Fact]
    public async Task A_failing_provisioner_leaves_the_stored_state_untouched()
    {
        _provisioner.LifecycleFailure = new InvalidOperationException("docker said no");
        var environment = TestEnvironments.Make(EnvironmentStatus.Stopped);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _lifecycle.StartAsync(environment, CancellationToken.None));

        Assert.Equal(EnvironmentStatus.Stopped, environment.Status);
        Assert.Equal(0, _repository.Upserts);
    }

    [Fact]
    public async Task A_missing_container_surfaces_as_not_found()
    {
        _provisioner.LifecycleFailure = new KeyNotFoundException();

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _lifecycle.RestartAsync(TestEnvironments.Make(), CancellationToken.None));
    }

    [Fact]
    public void The_conflict_message_names_the_action_and_status()
    {
        var ex = new InvalidEnvironmentStateException("start", EnvironmentStatus.Running);

        Assert.Equal("Can't start an environment that is Running.", ex.Message);
    }
}
