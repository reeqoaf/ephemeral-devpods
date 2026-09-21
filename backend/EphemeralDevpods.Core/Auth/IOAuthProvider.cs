using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Auth;

/// <summary>Authorization-code + PKCE flow against one external identity provider.</summary>
public interface IOAuthProvider
{
    IdentityProvider Provider { get; }

    Uri BuildAuthorizeUrl(string state, string codeChallenge, string redirectUri);

    Task<ExternalIdentity> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct);
}
