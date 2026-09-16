namespace EphemeralDevpods.Core.Git;

/// <summary>
/// An in-memory tar stream containing just the resolved build.context subtree, plus where the
/// Dockerfile ended up inside it — everything a Docker build API needs. The caller owns Tar and
/// must dispose it once the build completes.
/// </summary>
public sealed record BuildContext(Stream Tar, string DockerfilePathInContext);

/// <summary>
/// Fetches a repo's build context (or a subtree of it) as an in-memory tar stream, ready to hand
/// directly to a Docker build API — no filesystem involved (docs/spec.md §13/§14). GitHub-only
/// for v1.
/// </summary>
public interface IBuildContextFetcher
{
    /// <param name="repoUrl">The repo to fetch.</param>
    /// <param name="devcontainerBaseDirectory">
    /// EnvironmentSpec.DevcontainerBaseDirectory — contextPath and dockerfilePath are both
    /// resolved relative to this, independently of each other, not the repo root.
    /// </param>
    /// <param name="contextPath">EnvironmentSpec.BuildContextPath, as authored (e.g. ".", "..").</param>
    /// <param name="dockerfilePath">EnvironmentSpec.DockerfilePath, as authored.</param>
    /// <exception cref="BuildContextNotFoundException">
    /// contextPath matches no files, or dockerfilePath doesn't fall within the resolved context.
    /// </exception>
    Task<BuildContext> FetchAsync(
        string repoUrl, string devcontainerBaseDirectory, string contextPath, string dockerfilePath,
        CancellationToken ct);
}
