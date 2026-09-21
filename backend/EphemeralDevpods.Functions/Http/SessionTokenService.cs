using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EphemeralDevpods.Functions.Http;

public enum OAuthFlowMode
{
    Login,
    Link,
}

/// <summary>What the short-lived state cookie carries between the redirect to the provider and the callback.</summary>
public sealed record OAuthState(string State, string CodeVerifier, OAuthFlowMode Mode, string? UserId, string ReturnUrl);

/// <summary>
/// Issues and validates the two signed cookies: the long-lived `session` (identifies the user) and the
/// 10-minute `oauth_state` (binds a callback to the browser that started the flow). Both are HS256 JWTs but
/// with distinct audiences, so one can never be replayed as the other.
/// </summary>
public sealed class SessionTokenService(AuthOptions options, TimeProvider clock)
{
    public const string SessionCookie = "session";
    public const string StateCookie = "oauth_state";

    public static readonly TimeSpan SessionLifetime = TimeSpan.FromDays(7);
    public static readonly TimeSpan StateLifetime = TimeSpan.FromMinutes(10);

    private const string Issuer = "ephemeral-devpods";
    private const string SessionAudience = "session";
    private const string StateAudience = "oauth-state";

    private readonly SymmetricSecurityKey _key = new(Encoding.UTF8.GetBytes(options.SessionSigningKey));
    private readonly JsonWebTokenHandler _handler = new();

    public string CreateSession(string userId) =>
        Create(SessionAudience, SessionLifetime, new Dictionary<string, object> { ["sub"] = userId });

    /// <summary>Returns the user id, or null if the token is missing/forged/expired.</summary>
    public async Task<string?> ValidateSessionAsync(string token)
    {
        var claims = await ValidateAsync(token, SessionAudience);
        return claims is not null && claims.TryGetValue("sub", out var sub) ? sub as string : null;
    }

    public string CreateState(OAuthState state) => Create(StateAudience, StateLifetime, new Dictionary<string, object>
    {
        ["state"] = state.State,
        ["verifier"] = state.CodeVerifier,
        ["mode"] = state.Mode.ToString(),
        ["uid"] = state.UserId ?? "",
        ["ret"] = state.ReturnUrl,
    });

    public async Task<OAuthState?> ValidateStateAsync(string token)
    {
        var claims = await ValidateAsync(token, StateAudience);
        if (claims is null
            || claims["state"] is not string state
            || claims["verifier"] is not string verifier
            || claims["mode"] is not string mode
            || !Enum.TryParse<OAuthFlowMode>(mode, out var parsedMode)
            || claims["ret"] is not string returnUrl)
        {
            return null;
        }

        var uid = claims["uid"] as string;
        return new OAuthState(state, verifier, parsedMode, string.IsNullOrEmpty(uid) ? null : uid, returnUrl);
    }

    private string Create(string audience, TimeSpan lifetime, Dictionary<string, object> claims)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Issuer,
            Audience = audience,
            Claims = claims,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(lifetime),
            SigningCredentials = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256),
        });
    }

    private async Task<IDictionary<string, object>?> ValidateAsync(string token, string audience)
    {
        try
        {
            var result = await _handler.ValidateTokenAsync(token, new TokenValidationParameters
            {
                ValidIssuer = Issuer,
                ValidAudience = audience,
                IssuerSigningKey = _key,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ValidateLifetime = true,
                // Use the injected clock, not DateTime.UtcNow, so expiry is testable.
                LifetimeValidator = (_, expires, _, _) => expires is not null && expires > clock.GetUtcNow().UtcDateTime,
                ClockSkew = TimeSpan.Zero,
            });
            return result.IsValid ? result.Claims : null;
        }
        catch (Exception ex) when (ex is ArgumentException or SecurityTokenException)
        {
            return null;
        }
    }
}
