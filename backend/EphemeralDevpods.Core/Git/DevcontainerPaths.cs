namespace EphemeralDevpods.Core.Git;

/// <summary>
/// Path rules for devcontainer.json's build.context / build.dockerfile, shared by everything that needs to turn the
/// as-authored relative paths into repo-root-relative ones (docs/spec.md §14).
/// </summary>
public static class DevcontainerPaths
{
    /// <summary>
    /// Resolves a devcontainer.json-relative path (context or dockerfile — both resolved the same
    /// way, independently, per §14) against the repo root, manually handling "."/".." segments.
    /// Returns "" for the repo root.
    /// </summary>
    public static string Resolve(string baseDirectory, string relativePath)
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

    /// <returns>
    /// The path relative to the context folder, or null if the path falls outside it.
    /// </returns>
    public static string? ReparentUnderContext(string repoRelativePath, string resolvedContext)
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
}
