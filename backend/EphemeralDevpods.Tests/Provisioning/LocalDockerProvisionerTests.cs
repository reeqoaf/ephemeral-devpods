using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;

namespace EphemeralDevpods.Tests.Provisioning;

/// <summary>
/// Requires a running local Docker daemon (Docker Desktop). Exercises the image-only path
/// (no build) since that needs no network fetch beyond pulling a small, standard base image.
/// </summary>
public class LocalDockerProvisionerTests(DockerFixture docker) : IClassFixture<DockerFixture>
{
    private sealed class UnusedBuildContextFetcher : IBuildContextFetcher
    {
        public Task<BuildContext> FetchAsync(
            string repoUrl, string devcontainerBaseDirectory, string contextPath, string dockerfilePath,
            CancellationToken ct) =>
            throw new InvalidOperationException("Not expected to be called for an image-only spec.");
    }

    private LocalDockerProvisioner CreateProvisioner() => new(docker.Client, new UnusedBuildContextFetcher());

    private static WorkspaceEnvironment MakeEnvironment(string environmentId) => new()
    {
        Owner = "test-owner",
        EnvironmentId = environmentId,
        RepoUrl = "https://github.com/owner/repo",
        Status = EnvironmentStatus.Provisioning,
        TtlMinutes = 60,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Provisions_starts_and_tears_down_an_image_only_container()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment(Guid.NewGuid().ToString());
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [] };

        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);

        var resource = Assert.Single(result.Resources);
        Assert.Equal(ResourceType.Container, resource.Type);
        Assert.Equal("alpine:3.19", resource.Image);

        // alpine has no git/code — the entrypoint script fails fast, which is a real, deterministic
        // signal that container lifecycle + status mapping work, without needing a full VS Code
        // tunnel sign-in (impossible to automate) to prove anything end-to-end.
        await Task.Delay(TimeSpan.FromSeconds(2));
        var status = await provisioner.GetStatusAsync(environment.EnvironmentId, CancellationToken.None);
        Assert.Equal(EnvironmentStatus.Failed, status);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            provisioner.GetStatusAsync(environment.EnvironmentId, CancellationToken.None));
    }

    [Fact]
    public async Task Names_the_tunnel_after_the_environment_id()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment("1a2b3c4d-0000-0000-0000-000000000000");
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [] };

        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);

        Assert.Equal("epd-1a2b3c4d", result.TunnelName);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);
    }

    [Fact]
    public async Task Applies_the_requested_cpu_and_memory_limits()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment(Guid.NewGuid().ToString());
        environment.CpuCores = 2;
        environment.MemoryMb = 4096;
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [] };

        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);

        var inspected = await docker.Client.Containers.InspectContainerAsync(
            result.Resources[0].ResourceId, CancellationToken.None);
        Assert.Equal(4096L * 1024 * 1024, inspected.HostConfig.Memory);
        Assert.Equal(2_000_000_000L, inspected.HostConfig.NanoCPUs);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);
    }

    [Fact]
    public async Task Environments_without_limits_stay_unlimited()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment(Guid.NewGuid().ToString());
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [] };

        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);

        var inspected = await docker.Client.Containers.InspectContainerAsync(
            result.Resources[0].ResourceId, CancellationToken.None);
        Assert.Equal(0, inspected.HostConfig.Memory);
        Assert.Equal(0, inspected.HostConfig.NanoCPUs);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);
    }

    [Fact]
    public async Task Stop_start_and_restart_act_on_the_existing_container()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment(Guid.NewGuid().ToString());
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [] };
        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);
        var containerId = result.Resources[0].ResourceId;

        // alpine's entrypoint fails fast (no git), so the container is already exited here — which also
        // covers stopping something that is not running.
        await Task.Delay(TimeSpan.FromSeconds(2));
        await provisioner.StopAsync(environment.EnvironmentId, CancellationToken.None);

        await provisioner.StartAsync(environment.EnvironmentId, CancellationToken.None);
        var afterStart = await docker.Client.Containers.InspectContainerAsync(containerId, CancellationToken.None);
        Assert.True(afterStart.State.StartedAt is not null);

        await provisioner.RestartAsync(environment.EnvironmentId, CancellationToken.None);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);
    }

    [Fact]
    public async Task Lifecycle_actions_on_a_missing_container_throw_key_not_found()
    {
        var provisioner = CreateProvisioner();
        var missing = Guid.NewGuid().ToString();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => provisioner.StopAsync(missing, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => provisioner.StartAsync(missing, CancellationToken.None));
        await Assert.ThrowsAsync<KeyNotFoundException>(() => provisioner.RestartAsync(missing, CancellationToken.None));
    }

    [Fact]
    public async Task Publishes_each_port_at_the_chosen_host_port()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment(Guid.NewGuid().ToString());
        environment.PortMappings = [new PortMapping(3000, 38182)];
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [3000] };

        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);

        Assert.Equal("http://localhost:38182", result.PublicUrl);
        Assert.Equal(3000, result.Resources[0].Port); // the container-side port

        var inspected = await docker.Client.Containers.InspectContainerAsync(
            result.Resources[0].ResourceId, CancellationToken.None);
        Assert.Equal("38182", Assert.Single(inspected.HostConfig.PortBindings["3000/tcp"]).HostPort);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);
    }

    [Fact]
    public async Task Publishes_forwarded_ports_in_provision_result()
    {
        var provisioner = CreateProvisioner();
        var environment = MakeEnvironment(Guid.NewGuid().ToString());
        var spec = new EnvironmentSpec { Image = "alpine:3.19", ForwardPorts = [38080] };

        var result = await provisioner.ProvisionAsync(environment, spec, CancellationToken.None);

        Assert.Equal("http://localhost:38080", result.PublicUrl);
        Assert.Equal(38080, result.Resources[0].Port);

        await provisioner.TeardownAsync(environment.EnvironmentId, CancellationToken.None);
    }
}
