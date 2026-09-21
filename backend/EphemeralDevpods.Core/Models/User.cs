namespace EphemeralDevpods.Core.Models;

/// <summary>The application's own user. <see cref="UserId"/> is the stable owner key for environments.</summary>
public sealed class User
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}
