using System.Net;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Auth;

/// <summary>Shared plumbing for starting an OAuth redirect and issuing redirect responses.</summary>
public sealed class OAuthFlow(IEnumerable<IOAuthProvider> providers, SessionTokenService tokens, AuthOptions options)
{
    public const string StateCookiePath = "/api/auth";

    /// <summary>Maps the route segment to a provider; unknown names 404 via <see cref="KeyNotFoundException"/>.</summary>
    public static IdentityProvider ParseProvider(string name) => name.ToLowerInvariant() switch
    {
        "microsoft" => IdentityProvider.Microsoft,
        "github" => IdentityProvider.GitHub,
        _ => throw new KeyNotFoundException(),
    };

    public IOAuthProvider Resolve(string name)
    {
        var provider = ParseProvider(name);
        return providers.First(p => p.Provider == provider);
    }

    public HttpResponseData Start(HttpRequestData req, string providerName, OAuthFlowMode mode, string? userId, string? returnUrl)
    {
        var provider = Resolve(providerName);
        var verifier = Pkce.RandomToken(32);
        var state = Pkce.RandomToken(16);
        var destination = ReturnUrl.Sanitize(returnUrl, fallback: mode == OAuthFlowMode.Link ? "/settings" : "/");

        var authorizeUrl = provider.BuildAuthorizeUrl(state, Pkce.Challenge(verifier), options.RedirectUri(provider.Provider));

        var response = Redirect(req, authorizeUrl.AbsoluteUri);
        response.SetCookie(
            SessionTokenService.StateCookie,
            tokens.CreateState(new OAuthState(state, verifier, mode, userId, destination)),
            StateCookiePath,
            SessionTokenService.StateLifetime,
            options.SecureCookies);
        return response;
    }

    public static HttpResponseData Redirect(HttpRequestData req, string location)
    {
        var response = req.CreateResponse(HttpStatusCode.Found);
        response.Headers.Add("Location", location);
        return response;
    }
}
