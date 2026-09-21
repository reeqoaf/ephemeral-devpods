using System.Text.Json;
using System.Text.RegularExpressions;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace EphemeralDevpods.Infrastructure.Auth;

/// <summary>
/// Microsoft identity platform (v2.0) authorization-code + PKCE against the `common` authority, so both
/// personal and work/school accounts can sign in. `common` is multi-tenant, so the id_token issuer varies
/// per tenant and is validated by pattern rather than a fixed value.
/// </summary>
public sealed partial class MicrosoftOAuthProvider(HttpClient http, MicrosoftOAuthOptions options) : IOAuthProvider
{
    private const string Authority = "https://login.microsoftonline.com/common";

    // Static: providers are transient (typed HttpClient), and this caches the signing keys across requests.
    private static readonly ConfigurationManager<OpenIdConnectConfiguration> Metadata = new(
        $"{Authority}/v2.0/.well-known/openid-configuration",
        new OpenIdConnectConfigurationRetriever(),
        new HttpDocumentRetriever());

    public IdentityProvider Provider => IdentityProvider.Microsoft;

    public Uri BuildAuthorizeUrl(string state, string codeChallenge, string redirectUri)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["response_mode"] = "query",
            ["scope"] = "openid profile email",
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
            ["prompt"] = "select_account",
        };
        return new Uri($"{Authority}/oauth2/v2.0/authorize?{ToQueryString(query)}");
    }

    public async Task<ExternalIdentity> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        using var response = await http.PostAsync(
            $"{Authority}/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
                ["scope"] = "openid profile email",
            }),
            ct);

        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new OAuthExchangeException($"Microsoft token endpoint returned {(int)response.StatusCode}: {body}");
        }

        string? idToken;
        try
        {
            using var doc = JsonDocument.Parse(body);
            idToken = doc.RootElement.TryGetProperty("id_token", out var value) ? value.GetString() : null;
        }
        catch (JsonException ex)
        {
            throw new OAuthExchangeException("Microsoft token response was not valid JSON.", ex);
        }

        if (string.IsNullOrEmpty(idToken))
        {
            throw new OAuthExchangeException("Microsoft token response had no id_token.");
        }

        var config = await Metadata.GetConfigurationAsync(ct);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(idToken, new TokenValidationParameters
        {
            ValidateAudience = true,
            ValidAudience = options.ClientId,
            ValidateIssuer = true,
            IssuerValidator = (issuer, _, _) =>
                IsValidIssuer(issuer) ? issuer : throw new SecurityTokenInvalidIssuerException($"Unexpected issuer '{issuer}'."),
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = config.SigningKeys,
        });

        if (!result.IsValid)
        {
            throw new OAuthExchangeException("Microsoft id_token failed validation.", result.Exception);
        }

        string? Claim(string type) => result.Claims.TryGetValue(type, out var v) ? v?.ToString() : null;

        var subject = Claim("sub") ?? throw new OAuthExchangeException("Microsoft id_token had no sub claim.");
        var displayName = Claim("name") ?? Claim("preferred_username") ?? "Microsoft user";
        return new ExternalIdentity(IdentityProvider.Microsoft, subject, displayName, Claim("email"));
    }

    /// <summary>Accepts `https://login.microsoftonline.com/{tenant-guid}/v2.0` for any tenant, including the personal-account tenant.</summary>
    public static bool IsValidIssuer(string? issuer) => issuer is not null && IssuerPattern().IsMatch(issuer);

    [GeneratedRegex(@"^https://login\.microsoftonline\.com/[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}/v2\.0$")]
    private static partial Regex IssuerPattern();

    private static string ToQueryString(Dictionary<string, string> query) =>
        string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
}
