using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// App-level auth settings (config section "Auth"). Provider client ids/secrets live in
/// Auth:Microsoft / Auth:GitHub and are bound to the Infrastructure option classes.
/// </summary>
public sealed class AuthOptions
{
    public const int MinSigningKeyLength = 32;

    /// <summary>Origin the browser uses to reach the app. OAuth redirect URIs are built from this, never from the request Host.</summary>
    public string PublicBaseUrl { get; set; } = "http://localhost:5173";

    public string SessionSigningKey { get; set; } = "";

    public bool SecureCookies => PublicBaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    public string RedirectUri(IdentityProvider provider) =>
        $"{PublicBaseUrl.TrimEnd('/')}/api/auth/callback/{provider.ToString().ToLowerInvariant()}";
}
