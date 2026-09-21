namespace EphemeralDevpods.Core.Models;

public enum EnvironmentStatus
{
    Provisioning,
    Running,
    Expired,
    Failed,

    /// <summary>Container is stopped but kept, so it can be started again. Still subject to TTL expiry.</summary>
    Stopped,
}

/// <summary>Which account the environment's `code tunnel` authenticates with (device-code login).</summary>
public enum TunnelProvider
{
    GitHub,
    Microsoft,
}

/// <summary>Mirrors a row in the `environments` Table Storage table.</summary>
public sealed class WorkspaceEnvironment
{
    public required string Owner { get; init; }
    public required string EnvironmentId { get; init; }
    public required string RepoUrl { get; init; }

    /// <summary>User-facing name; null on environments created before names existed.</summary>
    public string? Name { get; set; }
    public EnvironmentStatus Status { get; set; }
    public required int TtlMinutes { get; set; }
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Container limits; null on environments created before limits existed (they run unlimited).</summary>
    public int? CpuCores { get; set; }
    public int? MemoryMb { get; set; }

    /// <summary>Null on environments created before the provider was selectable (they use GitHub).</summary>
    public TunnelProvider? TunnelProvider { get; set; }

    /// <summary>
    /// The host port each forwarded container port is published at (local Docker), kept for the environment's
    /// whole life (stopped included) so no other environment can be given the same port. Null on environments
    /// created before ports were selectable; those published each port at the same number on the host.
    /// </summary>
    public IReadOnlyList<PortMapping>? PortMappings { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string? PublicUrl { get; set; }
    public string? AccessToken { get; set; }

    /// <summary>Name the environment's `code tunnel` registers under (used to build editor URLs).</summary>
    public string? TunnelName { get; set; }

    /// <summary>Latched once the tunnel has connected, so ready environments need no further tunnel lookups.</summary>
    public bool TunnelReady { get; set; }
}
