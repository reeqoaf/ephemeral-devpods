namespace EphemeralDevpods.Core.Models;

public enum EnvironmentStatus
{
    Provisioning,
    Running,
    Expired,
    Failed,
}

/// <summary>Mirrors a row in the `environments` Table Storage table.</summary>
public sealed class WorkspaceEnvironment
{
    public required string Owner { get; init; }
    public required string EnvironmentId { get; init; }
    public required string RepoUrl { get; init; }
    public EnvironmentStatus Status { get; set; }
    public required int TtlMinutes { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string? PublicUrl { get; set; }
    public string? AccessToken { get; set; }

    /// <summary>Name the environment's `code tunnel` registers under (used to build editor URLs).</summary>
    public string? TunnelName { get; set; }

    /// <summary>Latched once the tunnel has connected, so ready environments need no further tunnel lookups.</summary>
    public bool TunnelReady { get; set; }
}
