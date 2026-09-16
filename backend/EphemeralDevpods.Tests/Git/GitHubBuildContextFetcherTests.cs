using System.Formats.Tar;
using System.IO.Compression;
using System.Net;
using System.Text;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Infrastructure.Git;

namespace EphemeralDevpods.Tests.Git;

public class GitHubBuildContextFetcherTests
{
    private sealed class StubHttpMessageHandler(byte[] gzippedTarball) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("https://api.github.com/repos/owner/repo/tarball", request.RequestUri!.ToString());
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(gzippedTarball),
            });
        }
    }

    // Mimics GitHub's real tarball shape: everything wrapped in one "{owner}-{repo}-{sha}/" folder.
    private static byte[] BuildFakeGitHubTarball(IReadOnlyDictionary<string, string> filesByPath)
    {
        using var tarStream = new MemoryStream();
        using (var writer = new TarWriter(tarStream, leaveOpen: true))
        {
            foreach (var (path, content) in filesByPath)
            {
                var entry = new PaxTarEntry(TarEntryType.RegularFile, $"owner-repo-abc123/{path}")
                {
                    DataStream = new MemoryStream(Encoding.UTF8.GetBytes(content)),
                };
                writer.WriteEntry(entry);
            }
        }

        tarStream.Position = 0;
        using var gzipOutput = new MemoryStream();
        using (var gzip = new GZipStream(gzipOutput, CompressionLevel.Fastest, leaveOpen: true))
        {
            tarStream.CopyTo(gzip);
        }

        return gzipOutput.ToArray();
    }

    private static HashSet<string> ReadEntryNames(Stream tarStream)
    {
        tarStream.Position = 0;
        using var reader = new TarReader(tarStream);
        var names = new HashSet<string>();
        while (reader.GetNextEntry() is { } entry)
        {
            names.Add(entry.Name);
        }

        return names;
    }

    private static readonly Dictionary<string, string> SampleRepo = new()
    {
        ["package.json"] = "{}",
        [".devcontainer/devcontainer.json"] = "{\"image\":\"node:20\"}",
        ["docker/Dockerfile.dev"] = "FROM node:20",
        ["docker/app.js"] = "console.log('hi')",
    };

    [Fact]
    public async Task Whole_repo_context_strips_github_wrapper_and_keeps_every_file()
    {
        var handler = new StubHttpMessageHandler(BuildFakeGitHubTarball(SampleRepo));
        var fetcher = new GitHubBuildContextFetcher(new HttpClient(handler));

        var context = await fetcher.FetchAsync(
            "https://github.com/owner/repo", devcontainerBaseDirectory: "", contextPath: ".",
            dockerfilePath: "docker/Dockerfile.dev", CancellationToken.None);

        await using (context.Tar)
        {
            var names = ReadEntryNames(context.Tar);
            Assert.Equal(
                new HashSet<string> { "package.json", ".devcontainer/devcontainer.json", "docker/Dockerfile.dev", "docker/app.js" },
                names);
            Assert.Equal("docker/Dockerfile.dev", context.DockerfilePathInContext);
        }
    }

    [Fact]
    public async Task Context_of_dotdot_from_devcontainer_folder_resolves_to_repo_root()
    {
        var handler = new StubHttpMessageHandler(BuildFakeGitHubTarball(SampleRepo));
        var fetcher = new GitHubBuildContextFetcher(new HttpClient(handler));

        var context = await fetcher.FetchAsync(
            "https://github.com/owner/repo", devcontainerBaseDirectory: ".devcontainer", contextPath: "..",
            dockerfilePath: "../docker/Dockerfile.dev", CancellationToken.None);

        await using (context.Tar)
        {
            var names = ReadEntryNames(context.Tar);
            Assert.Contains("package.json", names);
            Assert.Contains("docker/Dockerfile.dev", names);
            Assert.Equal("docker/Dockerfile.dev", context.DockerfilePathInContext);
        }
    }

    [Fact]
    public async Task Subdirectory_context_reparents_dockerfile_path_relative_to_it()
    {
        var handler = new StubHttpMessageHandler(BuildFakeGitHubTarball(SampleRepo));
        var fetcher = new GitHubBuildContextFetcher(new HttpClient(handler));

        var context = await fetcher.FetchAsync(
            "https://github.com/owner/repo", devcontainerBaseDirectory: "", contextPath: "docker",
            dockerfilePath: "docker/Dockerfile.dev", CancellationToken.None);

        await using (context.Tar)
        {
            var names = ReadEntryNames(context.Tar);
            Assert.Equal(new HashSet<string> { "Dockerfile.dev", "app.js" }, names);
            Assert.Equal("Dockerfile.dev", context.DockerfilePathInContext);
        }
    }

    [Fact]
    public async Task Throws_when_context_matches_nothing()
    {
        var handler = new StubHttpMessageHandler(BuildFakeGitHubTarball(SampleRepo));
        var fetcher = new GitHubBuildContextFetcher(new HttpClient(handler));

        await Assert.ThrowsAsync<BuildContextNotFoundException>(() =>
            fetcher.FetchAsync(
                "https://github.com/owner/repo", devcontainerBaseDirectory: "", contextPath: "does-not-exist",
                dockerfilePath: "does-not-exist/Dockerfile", CancellationToken.None));
    }

    [Fact]
    public async Task Throws_when_dockerfile_falls_outside_context()
    {
        var handler = new StubHttpMessageHandler(BuildFakeGitHubTarball(SampleRepo));
        var fetcher = new GitHubBuildContextFetcher(new HttpClient(handler));

        await Assert.ThrowsAsync<BuildContextNotFoundException>(() =>
            fetcher.FetchAsync(
                "https://github.com/owner/repo", devcontainerBaseDirectory: "", contextPath: "docker",
                dockerfilePath: "Dockerfile", CancellationToken.None));
    }

    [Fact]
    public async Task Returned_stream_can_be_disposed_after_use()
    {
        var handler = new StubHttpMessageHandler(BuildFakeGitHubTarball(SampleRepo));
        var fetcher = new GitHubBuildContextFetcher(new HttpClient(handler));

        var context = await fetcher.FetchAsync(
            "https://github.com/owner/repo", devcontainerBaseDirectory: "", contextPath: ".",
            dockerfilePath: "docker/Dockerfile.dev", CancellationToken.None);

        ReadEntryNames(context.Tar);
        await context.Tar.DisposeAsync();

        Assert.Throws<ObjectDisposedException>(() => context.Tar.ReadByte());
    }
}
