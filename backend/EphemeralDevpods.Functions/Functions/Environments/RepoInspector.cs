using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Parsing;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>What we learned about a repo's devcontainer, shared by the step-1 check and the create call.</summary>
public sealed record RepoInspection(string Owner, string Repo, EnvironmentSpec Spec)
{
    /// <summary>The devcontainer's own name if it has one, else the repo name.</summary>
    public string SuggestedName
    {
        get
        {
            var name = string.IsNullOrWhiteSpace(Spec.Name) ? Repo : Spec.Name.Trim();
            return name.Length > EnvironmentOptions.MaxNameLength ? name[..EnvironmentOptions.MaxNameLength] : name;
        }
    }
}

/// <summary>
/// Validates a repo URL end to end (github.com URL, devcontainer.json present, supported by the parser)
/// without provisioning anything. Failures are typed <c>UserInputException</c>s.
/// </summary>
public sealed class RepoInspector(IDevcontainerFileFetcher fileFetcher, IDevcontainerParser parser)
{
    public async Task<RepoInspection> InspectAsync(string repoUrl, CancellationToken ct)
    {
        var (owner, repo) = GitHubRepoUrl.Parse(repoUrl);
        var file = await fileFetcher.FetchAsync(repoUrl, ct) ?? throw new DevcontainerNotFoundException();

        return new RepoInspection(owner, repo, parser.Parse(file.Content, file.BaseDirectory));
    }
}
