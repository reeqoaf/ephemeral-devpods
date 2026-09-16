namespace EphemeralDevpods.Core.Parsing;

/// <summary>
/// The fetched devcontainer.json's raw text, plus which candidate location it was found at.
/// BaseDirectory matters downstream: build.context/dockerfile are relative to THIS folder, not
/// the repo root ("" for repo-root .devcontainer.json, ".devcontainer" for the nested location) —
/// docs/spec.md §14.
/// </summary>
public sealed record DevcontainerFile(string Content, string BaseDirectory);

/// <summary>
/// Stage 1 (docs/spec.md §14): fetches just the devcontainer.json file's raw text from a public
/// GitHub repo, without a full clone — tries .devcontainer/devcontainer.json then
/// .devcontainer.json at repo root.
/// </summary>
public interface IDevcontainerFileFetcher
{
    /// <returns>The fetched file, or null if neither candidate path exists.</returns>
    Task<DevcontainerFile?> FetchAsync(string repoUrl, CancellationToken ct);
}
