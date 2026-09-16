namespace EphemeralDevpods.Core.Models;

/// <summary>
/// Normalized deployment spec produced by parsing a repo's devcontainer.json.
/// BuildContextPath/DockerfilePath are relative, exactly as authored in the file — the parser
/// never touches a filesystem. Resolving them to real files is a provisioner-time concern
/// (see docs/spec.md §14).
/// </summary>
public sealed class EnvironmentSpec
{
    public string? Name { get; init; }
    public string? Image { get; init; }

    /// <summary>
    /// Folder devcontainer.json was found in ("" for repo root, ".devcontainer" for the nested
    /// location) — BuildContextPath/DockerfilePath below are relative to THIS, not the repo root.
    /// </summary>
    public string DevcontainerBaseDirectory { get; init; } = "";
    public string? BuildContextPath { get; init; }
    public string? DockerfilePath { get; init; }
    public IReadOnlyDictionary<string, string> BuildArgs { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<int> ForwardPorts { get; init; } = [];
    public IReadOnlyDictionary<string, string> ContainerEnv { get; init; } =
        new Dictionary<string, string>();
    public IReadOnlyList<string> PostCreateCommand { get; init; } = [];

    /// <summary>
    /// Run in the background (non-blocking), after PostCreateCommand, before the container's
    /// main process starts — the conventional place devcontainer.json puts a dev-server start
    /// command, since PostCreateCommand is meant to finish (setup), not run forever (§14).
    /// </summary>
    public IReadOnlyList<string> PostAttachCommand { get; init; } = [];
}
