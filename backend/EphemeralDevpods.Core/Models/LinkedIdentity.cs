namespace EphemeralDevpods.Core.Models;

/// <summary>An external identity attached to a <see cref="User"/>. One external identity belongs to exactly one user.</summary>
public sealed class LinkedIdentity
{
    public required IdentityProvider Provider { get; init; }
    public required string Subject { get; init; }
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public DateTimeOffset LinkedAt { get; init; }
}
