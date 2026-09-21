using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Auth;

/// <summary>Caller is not authenticated (no or invalid session). Maps to 401.</summary>
public sealed class UnauthorizedException(string message = "Authentication required.") : Exception(message);

/// <summary>Base for failures caused by a conflict with existing state; the message is safe to return as a 409.</summary>
public abstract class ConflictException(string message) : Exception(message);

public sealed class IdentityAlreadyLinkedException(IdentityProvider provider)
    : ConflictException($"That {provider} account is already linked to a different user.");

/// <summary>The provider rejected the code exchange or returned an unusable identity. The message is for logs, not callers.</summary>
public sealed class OAuthExchangeException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>An account rule was violated (e.g. unlinking the root Microsoft identity).</summary>
public sealed class AccountRuleException(string message) : UserInputException(message);

/// <summary>A sign-in via a provider that can't create accounts, with an identity that isn't linked to any user.</summary>
public sealed class IdentityNotLinkedException(IdentityProvider provider)
    : UserInputException($"That {provider} account isn't linked to any user.");
