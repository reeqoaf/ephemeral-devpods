using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Parsing;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class RepoInspectorTests
{
    private sealed class FakeFileFetcher(DevcontainerFile? file) : IDevcontainerFileFetcher
    {
        public Task<DevcontainerFile?> FetchAsync(string repoUrl, CancellationToken ct) => Task.FromResult(file);
    }

    private static RepoInspector InspectorFor(DevcontainerFile? file) =>
        new(new FakeFileFetcher(file), new DevcontainerParser());

    [Fact]
    public async Task Returns_owner_repo_and_the_parsed_spec()
    {
        var inspector = InspectorFor(new DevcontainerFile(
            """{ "image": "mcr.microsoft.com/devcontainers/base:ubuntu", "forwardPorts": [3000] }""", ".devcontainer"));

        var inspection = await inspector.InspectAsync("https://github.com/octo/hello.git", CancellationToken.None);

        Assert.Equal("octo", inspection.Owner);
        Assert.Equal("hello", inspection.Repo);
        Assert.Equal("mcr.microsoft.com/devcontainers/base:ubuntu", inspection.Spec.Image);
        Assert.Equal([3000], inspection.Spec.ForwardPorts);
    }

    [Fact]
    public async Task Suggests_the_devcontainers_own_name_when_it_has_one()
    {
        var inspector = InspectorFor(new DevcontainerFile("""{ "name": "Web app", "image": "alpine" }""", ""));

        var inspection = await inspector.InspectAsync("https://github.com/octo/hello", CancellationToken.None);

        Assert.Equal("Web app", inspection.SuggestedName);
    }

    [Fact]
    public async Task Suggests_the_repo_name_otherwise()
    {
        var inspector = InspectorFor(new DevcontainerFile("""{ "image": "alpine" }""", ""));

        var inspection = await inspector.InspectAsync("https://github.com/octo/hello", CancellationToken.None);

        Assert.Equal("hello", inspection.SuggestedName);
    }

    [Fact]
    public async Task A_suggested_name_is_capped_to_the_name_limit()
    {
        var longName = new string('n', EnvironmentOptions.MaxNameLength + 20);
        var inspector = InspectorFor(new DevcontainerFile($$"""{ "name": "{{longName}}", "image": "alpine" }""", ""));

        var inspection = await inspector.InspectAsync("https://github.com/octo/hello", CancellationToken.None);

        Assert.Equal(EnvironmentOptions.MaxNameLength, inspection.SuggestedName.Length);
    }

    [Fact]
    public async Task A_repo_without_a_devcontainer_is_a_user_input_error()
    {
        var inspector = InspectorFor(null);

        await Assert.ThrowsAsync<DevcontainerNotFoundException>(
            () => inspector.InspectAsync("https://github.com/octo/hello", CancellationToken.None));
    }

    [Fact]
    public async Task A_non_github_url_is_rejected_before_fetching()
    {
        var inspector = InspectorFor(new DevcontainerFile("""{ "image": "alpine" }""", ""));

        await Assert.ThrowsAsync<InvalidRepoUrlException>(
            () => inspector.InspectAsync("https://gitlab.com/octo/hello", CancellationToken.None));
    }

    [Fact]
    public async Task An_unsupported_devcontainer_is_a_parse_error()
    {
        var inspector = InspectorFor(new DevcontainerFile("""{ "dockerComposeFile": "compose.yml" }""", ""));

        await Assert.ThrowsAsync<DevcontainerParseException>(
            () => inspector.InspectAsync("https://github.com/octo/hello", CancellationToken.None));
    }
}
