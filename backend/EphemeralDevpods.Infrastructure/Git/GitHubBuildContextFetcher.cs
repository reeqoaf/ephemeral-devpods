using System.Formats.Tar;
using System.IO.Compression;
using EphemeralDevpods.Core.Git;

namespace EphemeralDevpods.Infrastructure.Git;

/// <summary>
/// Downloads a repo as a tarball straight into memory (GitHub's archive endpoint), re-packages
/// just the resolved build.context subtree into a fresh in-memory tar stream, and hands that to
/// the caller — no filesystem touched at any point (docs/spec.md §13/§14).
/// </summary>
public sealed class GitHubBuildContextFetcher(HttpClient httpClient) : IBuildContextFetcher
{
    public async Task<BuildContext> FetchAsync(
        string repoUrl, string devcontainerBaseDirectory, string contextPath, string dockerfilePath,
        CancellationToken ct)
    {
        var (owner, repo) = GitHubRepoUrl.Parse(repoUrl);
        var resolvedContext = ResolvePath(devcontainerBaseDirectory, contextPath);
        var resolvedDockerfile = ResolvePath(devcontainerBaseDirectory, dockerfilePath);

        var dockerfilePathInContext = ReparentUnderContext(resolvedDockerfile, resolvedContext);
        if (dockerfilePathInContext is null)
        {
            throw new BuildContextNotFoundException(
                $"Dockerfile \"{dockerfilePath}\" is not located within build context \"{contextPath}\" in {repoUrl}.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/tarball");
        request.Headers.Add("User-Agent", "ephemeral-devpods");

        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var compressedStream = await response.Content.ReadAsStreamAsync(ct);
        await using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
        using var tarReader = new TarReader(gzipStream);

        var output = new MemoryStream();
        var matchedAny = false;

        await using (var writer = new TarWriter(output, leaveOpen: true))
        {
            while (await tarReader.GetNextEntryAsync(copyData: true, ct) is { } entry)
            {
                // Entry names look like "{owner}-{repo}-{sha}/relative/path" — strip that wrapper.
                var slashIndex = entry.Name.IndexOf('/');
                var relativePath = slashIndex >= 0 ? entry.Name[(slashIndex + 1)..] : "";

                if (relativePath.Length == 0)
                {
                    continue; // the wrapper directory entry itself
                }

                var newName = ReparentUnderContext(relativePath, resolvedContext);
                if (string.IsNullOrEmpty(newName))
                {
                    continue;
                }

                matchedAny = true;
                entry.Name = newName;
                await writer.WriteEntryAsync(entry, ct);
            }
        }

        if (!matchedAny)
        {
            await output.DisposeAsync();
            throw new BuildContextNotFoundException(
                $"No files found under build context \"{contextPath}\" (resolved to \"/{resolvedContext}\") in {repoUrl}.");
        }

        output.Position = 0;
        return new BuildContext(output, dockerfilePathInContext);
    }

    /// <returns>
    /// The path relative to the context folder, or null if the entry falls outside it.
    /// </returns>
    private static string? ReparentUnderContext(string repoRelativePath, string resolvedContext)
    {
        if (resolvedContext.Length == 0)
        {
            return repoRelativePath;
        }

        if (repoRelativePath == resolvedContext ||
            !repoRelativePath.StartsWith(resolvedContext + "/", StringComparison.Ordinal))
        {
            return null;
        }

        return repoRelativePath[(resolvedContext.Length + 1)..];
    }

    /// <summary>
    /// Resolves a devcontainer.json-relative path (context or dockerfile — both resolved the same
    /// way, independently, per §14) against the repo root, manually handling "."/".." segments.
    /// Returns "" for the repo root.
    /// </summary>
    private static string ResolvePath(string baseDirectory, string relativePath)
    {
        var combined = string.IsNullOrEmpty(baseDirectory) ? relativePath : $"{baseDirectory}/{relativePath}";
        var stack = new List<string>();

        foreach (var segment in combined.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    break;
                case "..":
                    if (stack.Count > 0)
                    {
                        stack.RemoveAt(stack.Count - 1);
                    }

                    break;
                default:
                    stack.Add(segment);
                    break;
            }
        }

        return string.Join('/', stack);
    }
}
