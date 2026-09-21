using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class EnvironmentResponseTests
{
    [Fact]
    public void Exposes_name_limits_provider_and_expiry()
    {
        var env = TestEnvironments.Make();
        env.Name = "My pod";
        env.CpuCores = 2;
        env.MemoryMb = 4096;
        env.TunnelProvider = TunnelProvider.GitHub;

        var response = EnvironmentResponse.From(env);

        Assert.Equal("My pod", response.Name);
        Assert.Equal(2, response.CpuCores);
        Assert.Equal(4096, response.MemoryMb);
        Assert.Equal("GitHub", response.TunnelProvider);
        Assert.Equal(env.CreatedAt.AddMinutes(60), response.ExpiresAt);
    }

    [Fact]
    public void Environments_from_before_names_and_limits_report_nulls_and_github()
    {
        var response = EnvironmentResponse.From(TestEnvironments.Make());

        Assert.Null(response.Name);
        Assert.Null(response.CpuCores);
        Assert.Null(response.MemoryMb);
        Assert.Equal("GitHub", response.TunnelProvider);
    }

    [Fact]
    public void Exposes_port_mappings_and_none_for_older_environments()
    {
        var env = TestEnvironments.Make();
        Assert.Empty(EnvironmentResponse.From(env).PortMappings);

        env.PortMappings = [new PortMapping(3000, 8081)];
        Assert.Equal([new PortMapping(3000, 8081)], EnvironmentResponse.From(env).PortMappings);
    }

    [Fact]
    public void A_stopped_environment_has_no_tunnel()
    {
        var response = EnvironmentResponse.From(TestEnvironments.Make(EnvironmentStatus.Stopped, tunnelReady: true));

        Assert.Equal("Stopped", response.Status);
        Assert.Null(response.Tunnel);
    }
}
