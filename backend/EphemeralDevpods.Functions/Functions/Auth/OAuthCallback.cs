using System.Security.Cryptography;
using System.Text;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EphemeralDevpods.Functions.Functions.Auth;

/// <summary>
/// The provider redirects the browser here with a code. The browser is mid-navigation, so every outcome —
/// including expected failures — is a redirect back into the SPA (/login or /settings, with ?error=), not JSON.
/// </summary>
public sealed class OAuthCallback(
    OAuthFlow flow, AccountService accounts, SessionTokenService tokens, AuthOptions options, ILogger<OAuthCallback> logger)
{
    [Function("OAuthCallback")]
    [AllowAnonymousAccess]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/callback/{provider}")] HttpRequestData req,
        string provider, FunctionContext context, CancellationToken ct)
    {
        var oauthProvider = flow.Resolve(provider);

        var stateCookie = req.GetCookie(SessionTokenService.StateCookie);
        var saved = stateCookie is null ? null : await tokens.ValidateStateAsync(stateCookie);
        var returnedState = req.Query["state"];

        // The state cookie is single-use: always clear it, whatever happens next.
        HttpResponseData Finish(HttpResponseData response)
        {
            response.ClearCookie(SessionTokenService.StateCookie, OAuthFlow.StateCookiePath, options.SecureCookies);
            return response;
        }

        if (saved is null || returnedState is null || !StateMatches(saved.State, returnedState))
        {
            return Finish(OAuthFlow.Redirect(req, "/login?error=invalid_state"));
        }

        var failureTarget = saved.Mode == OAuthFlowMode.Link ? "/settings" : "/login";
        var code = req.Query["code"];
        if (req.Query["error"] is not null || string.IsNullOrEmpty(code))
        {
            return Finish(OAuthFlow.Redirect(req, $"{failureTarget}?error=access_denied"));
        }

        ExternalIdentity identity;
        try
        {
            identity = await oauthProvider.ExchangeAsync(code, saved.CodeVerifier, options.RedirectUri(oauthProvider.Provider), ct);
        }
        catch (OAuthExchangeException ex)
        {
            logger.LogWarning(ex, "OAuth code exchange failed for {Provider}", oauthProvider.Provider);
            return Finish(OAuthFlow.Redirect(req, $"{failureTarget}?error=provider_error"));
        }

        return saved.Mode == OAuthFlowMode.Link
            ? Finish(await CompleteLinkAsync(req, context, saved, identity, ct))
            : Finish(await CompleteLoginAsync(req, oauthProvider.Provider, saved, identity, ct));
    }

    private async Task<HttpResponseData> CompleteLoginAsync(
        HttpRequestData req, IdentityProvider provider, OAuthState saved, ExternalIdentity identity, CancellationToken ct)
    {
        User user;
        try
        {
            user = await accounts.SignInAsync(identity, ct);
        }
        catch (IdentityNotLinkedException)
        {
            return OAuthFlow.Redirect(req, $"/login?error={provider.ToString().ToLowerInvariant()}_not_linked");
        }

        var response = OAuthFlow.Redirect(req, saved.ReturnUrl);
        response.SetCookie(
            SessionTokenService.SessionCookie, tokens.CreateSession(user.UserId), "/",
            SessionTokenService.SessionLifetime, options.SecureCookies);
        return response;
    }

    private async Task<HttpResponseData> CompleteLinkAsync(
        HttpRequestData req, FunctionContext context, OAuthState saved, ExternalIdentity identity, CancellationToken ct)
    {
        // The callback is anonymous-allowed (login needs it), so verify the session ourselves: the user who
        // started the link must still be the signed-in user, or the identity could be attached to the wrong account.
        var currentUser = CurrentUser.TryGetId(context);
        if (currentUser is null || currentUser != saved.UserId)
        {
            return OAuthFlow.Redirect(req, "/login?error=session_mismatch");
        }

        try
        {
            await accounts.LinkAsync(currentUser, identity, ct);
        }
        catch (IdentityAlreadyLinkedException)
        {
            return OAuthFlow.Redirect(req, "/settings?error=already_linked");
        }
        catch (AccountRuleException)
        {
            return OAuthFlow.Redirect(req, "/settings?error=provider_already_linked");
        }

        return OAuthFlow.Redirect(req, saved.ReturnUrl);
    }

    private static bool StateMatches(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
