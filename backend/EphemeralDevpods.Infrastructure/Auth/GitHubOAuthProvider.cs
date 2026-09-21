using System.Net.Http.Headers;
using System.Text.Json;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Auth;

/// <summary>
/// GitHub OAuth App authorization-code + PKCE. No scopes are requested: only the public profile is needed to
/// identify the account, and the resulting access token is discarded (nothing is stored or used afterwards).
/// </summary>
public sealed class GitHubOAuthProvider(HttpClient http, GitHubOAuthOptions options) : IOAuthProvider
{
    public IdentityProvider Provider => IdentityProvider.GitHub;

    public Uri BuildAuthorizeUrl(string state, string codeChallenge, string redirectUri)
    {
        var query = new Dictionary<string, string>
        {
            ["client_id"] = options.ClientId,
            ["redirect_uri"] = redirectUri,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };
        var queryString = string.Join("&", query.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return new Uri($"https://github.com/login/oauth/authorize?{queryString}");
    }

    public async Task<ExternalIdentity> ExchangeAsync(string code, string codeVerifier, string redirectUri, CancellationToken ct)
    {
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ClientId,
                ["client_secret"] = options.ClientSecret,
                ["code"] = code,
                ["redirect_uri"] = redirectUri,
                ["code_verifier"] = codeVerifier,
            }),
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var tokenResponse = await http.SendAsync(tokenRequest, ct);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync(ct);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new OAuthExchangeException($"GitHub token endpoint returned {(int)tokenResponse.StatusCode}: {tokenBody}");
        }

        string? accessToken;
        try
        {
            // GitHub answers 200 even for failures, with { "error": ... } in the body.
            using var doc = JsonDocument.Parse(tokenBody);
            accessToken = doc.RootElement.TryGetProperty("access_token", out var value) ? value.GetString() : null;
        }
        catch (JsonException ex)
        {
            throw new OAuthExchangeException("GitHub token response was not valid JSON.", ex);
        }

        if (string.IsNullOrEmpty(accessToken))
        {
            throw new OAuthExchangeException($"GitHub token response had no access_token: {tokenBody}");
        }

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        userRequest.Headers.Add("User-Agent", "ephemeral-devpods");
        userRequest.Headers.Add("Accept", "application/vnd.github+json");

        using var userResponse = await http.SendAsync(userRequest, ct);
        if (!userResponse.IsSuccessStatusCode)
        {
            throw new OAuthExchangeException($"GitHub /user returned {(int)userResponse.StatusCode}.");
        }

        using var userDoc = JsonDocument.Parse(await userResponse.Content.ReadAsStringAsync(ct));
        var root = userDoc.RootElement;
        if (!root.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var id))
        {
            throw new OAuthExchangeException("GitHub /user response had no id.");
        }

        var login = root.TryGetProperty("login", out var loginElement) ? loginElement.GetString() : null;
        var email = root.TryGetProperty("email", out var emailElement) && emailElement.ValueKind == JsonValueKind.String
            ? emailElement.GetString()
            : null;
        return new ExternalIdentity(IdentityProvider.GitHub, id.ToString(), login ?? "GitHub user", email);
    }
}
