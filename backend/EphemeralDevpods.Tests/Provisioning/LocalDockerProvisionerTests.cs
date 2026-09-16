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
