namespace EphemeralDevpods.Core.Models;

/// <summary>An identity asserted by an OAuth provider. <see cref="Subject"/> is the provider's stable id for the account.</summary>
public sealed record ExternalIdentity(IdentityProvider Provider, string Subject, string DisplayName, string? Email);
