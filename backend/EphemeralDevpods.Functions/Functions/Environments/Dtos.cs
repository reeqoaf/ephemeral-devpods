using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Functions.Functions.Environments;

public sealed record CreateEnvironmentRequest(string? RepoUrl);

/// <summary>What the frontend's WorkspaceEnvironment TS type expects — deliberately excludes
/// AccessToken, which the container itself uses, not the dashboard.</summary>
public sealed record EnvironmentResponse(
    string EnvironmentId, string Owner, string RepoUrl, string Status, int TtlMinutes,
    DateTimeOffset CreatedAt, string? PublicUrl)
{
    public static EnvironmentResponse From(WorkspaceEnvironment env) => new(
        env.EnvironmentId, env.Owner, env.RepoUrl, env.Status.ToString(), env.TtlMinutes, env.CreatedAt, env.PublicUrl);
}
