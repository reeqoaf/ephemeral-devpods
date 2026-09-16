using Docker.DotNet;

namespace EphemeralDevpods.Tests.Provisioning;

/// <summary>Connects to the locally running Docker daemon (Docker Desktop) for integration tests.</summary>
public sealed class DockerFixture : IDisposable
{
    public IDockerClient Client { get; }

    public DockerFixture()
    {
        var uri = OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");

        Client = new DockerClientConfiguration(uri).CreateClient();
    }

    public void Dispose() => Client.Dispose();
}
