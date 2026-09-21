using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Parsing;

namespace EphemeralDevpods.Infrastructure.Git;

/// <summary>
/// Fetches devcontainer.json via the GitHub contents API (resolves the default branch
/// automatically), trying .devcontainer/devcontainer.json then .devcontainer.json.
/// GitHub-only for v1 (docs/spec.md §14).
/// </summary>
public sealed class GitHubDevcontainerFetcher(HttpClient httpClient) : IDevcontainerFileFetcher
{
    // (candidate path, base directory build.context/dockerfile are resolved against — §14)
    private static readonly (string Path, string BaseDirectory)[] Candidates =
    [
        (".devcontainer/devcontainer.json", ".devcontainer"),
        (".devcontainer.json", ""),
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<DevcontainerFile?> FetchAsync(string repoUrl, CancellationToken ct)
    {
        var (owner, repo) = GitHubRepoUrl.Parse(repoUrl);

        foreach (var (path, baseDirectory) in Candidates)
        {
            var content = await TryFetchFileAsync(owner, repo, path, ct);
            if (content is not null)
            {
                return new DevcontainerFile(content, baseDirectory);
            }
        }

        return null;
    }

    private async Task<string?> TryFetchFileAsync(string owner, string repo, string path, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"https://api.github.com/repos/{owner}/{repo}/contents/{path}");
        request.Headers.Add("User-Agent", "ephemeral-devpods");
        request.Headers.Add("Accept", "application/vnd.github+json");

        using var response = await httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        var stream = await response.Content.ReadAsStreamAsync(ct);
        await using (stream.ConfigureAwait(false))
        {
            var payload = await JsonSerializer.DeserializeAsync<GitHubContentsResponse>(stream, JsonOptions, ct);
            if (payload?.Content is null)
            {
                return null;
            }

            var bytes = Convert.FromBase64String(payload.Content.Replace("\n", ""));
            return Encoding.UTF8.GetString(bytes);
        }
    }

    private sealed class GitHubContentsResponse
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }
}
