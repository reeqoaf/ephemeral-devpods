namespace EphemeralDevpods.Core.Models;

/// <summary>The application's own user. <see cref="UserId"/> is the stable owner key for environments.</summary>
public sealed class User
{
    public required string UserId { get; init; }
    public required string DisplayName { get; init; }
    public string? Email { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Only admins may run environments on the cloud backend (the abuse control, since anyone can sign up).
    /// There is no in-app way to grant it: set the property on the user's row in the `users` table by hand.
    /// </summary>
    public bool IsAdmin { get; init; }
}
