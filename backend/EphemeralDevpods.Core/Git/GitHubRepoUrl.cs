namespace EphemeralDevpods.Core.Git;

/// <summary>Parses a github.com repo URL into owner/repo (used by the fetchers and the repo check).</summary>
public static class GitHubRepoUrl
{
    public static (string Owner, string Repo) Parse(string repoUrl)
    {
        if (!Uri.TryCreate(repoUrl, UriKind.Absolute, out var uri) ||
            !uri.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidRepoUrlException($"Only github.com repo URLs are supported (got: {repoUrl}).");
        }

        var segments = uri.AbsolutePath.Trim('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            throw new InvalidRepoUrlException($"Could not parse owner/repo from URL: {repoUrl}");
        }

        var owner = segments[0];
        var repo = segments[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? segments[1][..^4]
            : segments[1];

        return (owner, repo);
    }
}
