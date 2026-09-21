using System.Net;
using System.Text;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Infrastructure.Git;

namespace EphemeralDevpods.Tests.Git;

public class GitHubDevcontainerFetcherTests
{
    private static string Base64(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        : HttpMessageHandler
    {
        public List<Uri> RequestedUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUris.Add(request.RequestUri!);
            return Task.FromResult(handler(request));
        }
    }

    [Fact]
    public async Task Fetches_devcontainer_json_from_dot_devcontainer_folder()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            Assert.Equal(
                "https://api.github.com/repos/owner/repo/contents/.devcontainer/devcontainer.json",
                req.RequestUri!.ToString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{ "content": "{{Base64("{\"image\":\"node:20\"}")}}" }"""),
            };
        });

        var fetcher = new GitHubDevcontainerFetcher(new HttpClient(handler));
        var file = await fetcher.FetchAsync("https://github.com/owner/repo", CancellationToken.None);

        Assert.Equal("""{"image":"node:20"}""", file?.Content);
        Assert.Equal(".devcontainer", file?.BaseDirectory);
    }

    [Fact]
    public async Task Falls_back_to_root_devcontainer_json_when_first_path_missing()
    {
        var handler = new StubHttpMessageHandler(req =>
            req.RequestUri!.ToString().EndsWith(".devcontainer/devcontainer.json")
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent($$"""{ "content": "{{Base64("{\"image\":\"node:20\"}")}}" }"""),
                });

        var fetcher = new GitHubDevcontainerFetcher(new HttpClient(handler));
        var file = await fetcher.FetchAsync("https://github.com/owner/repo", CancellationToken.None);

        Assert.Equal("""{"image":"node:20"}""", file?.Content);
        Assert.Equal("", file?.BaseDirectory);
        Assert.Equal(2, handler.RequestedUris.Count);
    }

    [Fact]
    public async Task Returns_null_when_neither_path_exists()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        var fetcher = new GitHubDevcontainerFetcher(new HttpClient(handler));
        var file = await fetcher.FetchAsync("https://github.com/owner/repo", CancellationToken.None);

        Assert.Null(file);
    }

    [Fact]
    public async Task Strips_git_suffix_from_repo_name()
    {
        var handler = new StubHttpMessageHandler(req =>
        {
            Assert.Contains("/repos/owner/repo/contents/", req.RequestUri!.ToString());
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var fetcher = new GitHubDevcontainerFetcher(new HttpClient(handler));
        await fetcher.FetchAsync("https://github.com/owner/repo.git", CancellationToken.None);
    }

    [Fact]
    public async Task Throws_for_non_github_url()
    {
        var fetcher = new GitHubDevcontainerFetcher(new HttpClient(new StubHttpMessageHandler(
            _ => new HttpResponseMessage(HttpStatusCode.NotFound))));

        await Assert.ThrowsAsync<InvalidRepoUrlException>(() =>
            fetcher.FetchAsync("https://gitlab.com/owner/repo", CancellationToken.None));
    }
}
