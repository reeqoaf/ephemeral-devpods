using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class HostPortSelectorTests
{
    private readonly FakeRepository _repository = new();
    private readonly HostPortSelector _selector;

    public HostPortSelectorTests()
    {
        _selector = new HostPortSelector(_repository);
    }

    private static WorkspaceEnvironment EnvironmentHolding(EnvironmentStatus status, params PortMapping[] mappings)
    {
        var environment = TestEnvironments.Make(status);
        environment.PortMappings = mappings;
        return environment;
    }

    [Fact]
    public async Task Nothing_requested_means_each_port_at_its_own_number()
    {
        var resolved = await _selector.ResolveAsync([3000, 5173], requested: null, CancellationToken.None);

        Assert.Equal([new PortMapping(3000, 3000), new PortMapping(5173, 5173)], resolved);
    }

    [Fact]
    public async Task The_default_ports_are_checked_against_other_environments_too()
    {
        _repository.Active.Add(EnvironmentHolding(EnvironmentStatus.Stopped, new PortMapping(3000, 3000)));

        await Assert.ThrowsAsync<HostPortInUseException>(() =>
            _selector.ResolveAsync([3000], requested: null, CancellationToken.None));
    }

    [Fact]
    public async Task Well_known_ports_are_allowed()
    {
        var resolved = await _selector.ResolveAsync([80], requested: null, CancellationToken.None);

        Assert.Equal(80, Assert.Single(resolved).HostPort);
    }

    [Fact]
    public async Task A_chosen_port_is_used_as_given()
    {
        var resolved = await _selector.ResolveAsync([3000], [new PortMapping(3000, 8081)], CancellationToken.None);

        Assert.Equal([new PortMapping(3000, 8081)], resolved);
    }

    [Fact]
    public async Task Resolved_mappings_follow_the_repos_port_order()
    {
        var resolved = await _selector.ResolveAsync(
            [3000, 5173], [new PortMapping(5173, 9001), new PortMapping(3000, 9000)], CancellationToken.None);

        Assert.Equal([new PortMapping(3000, 9000), new PortMapping(5173, 9001)], resolved);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(65536)]
    public async Task Ports_outside_the_valid_range_are_rejected(int hostPort)
    {
        await Assert.ThrowsAsync<InvalidEnvironmentOptionException>(() =>
            _selector.ResolveAsync([3000], [new PortMapping(3000, hostPort)], CancellationToken.None));
    }

    [Fact]
    public async Task Mappings_that_dont_match_the_forwarded_ports_are_rejected()
    {
        await Assert.ThrowsAsync<InvalidEnvironmentOptionException>(() =>
            _selector.ResolveAsync([3000], [new PortMapping(4000, 8081)], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidEnvironmentOptionException>(() =>
            _selector.ResolveAsync([3000, 5173], [new PortMapping(3000, 8081)], CancellationToken.None));
        await Assert.ThrowsAsync<InvalidEnvironmentOptionException>(() =>
            _selector.ResolveAsync([], [new PortMapping(3000, 8081)], CancellationToken.None));
    }

    [Fact]
    public async Task Two_ports_cant_share_a_host_port()
    {
        await Assert.ThrowsAsync<InvalidEnvironmentOptionException>(() => _selector.ResolveAsync(
            [3000, 5173], [new PortMapping(3000, 8081), new PortMapping(5173, 8081)], CancellationToken.None));
    }

    [Theory]
    [InlineData(EnvironmentStatus.Running)]
    [InlineData(EnvironmentStatus.Provisioning)]
    [InlineData(EnvironmentStatus.Stopped)] // not listening, but it needs the port back when started
    [InlineData(EnvironmentStatus.Failed)]
    public async Task A_port_held_by_another_environment_is_refused(EnvironmentStatus status)
    {
        _repository.Active.Add(EnvironmentHolding(status, new PortMapping(3000, 8081)));

        var ex = await Assert.ThrowsAsync<HostPortInUseException>(() =>
            _selector.ResolveAsync([3000], [new PortMapping(3000, 8081)], CancellationToken.None));

        Assert.Contains("8081", ex.Message);
    }

    [Fact]
    public async Task Other_environments_ports_that_dont_clash_are_fine()
    {
        _repository.Active.Add(EnvironmentHolding(EnvironmentStatus.Running, new PortMapping(3000, 8081)));

        var resolved = await _selector.ResolveAsync([3000], [new PortMapping(3000, 8082)], CancellationToken.None);

        Assert.Equal(8082, Assert.Single(resolved).HostPort);
    }

    [Fact]
    public async Task Environments_from_before_ports_were_selectable_hold_no_recorded_ports()
    {
        _repository.Active.Add(TestEnvironments.Make()); // PortMappings is null

        var resolved = await _selector.ResolveAsync([3000], [new PortMapping(3000, 8081)], CancellationToken.None);

        Assert.Single(resolved);
    }
}
