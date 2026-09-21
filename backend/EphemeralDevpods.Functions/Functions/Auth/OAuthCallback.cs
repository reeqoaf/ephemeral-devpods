using System.Security.Cryptography;
using System.Text;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Auth;

/// <summary>
/// The provider redirects the browser here with a code. The browser is mid-navigation, so every outcome —
/// including failures — is a redirect back into the SPA (/login or /settings, with ?error=), not JSON.
/// Exceptions are turned into those redirects by <see cref="ExceptionHandlingMiddleware"/> via <see cref="ErrorRedirect"/>.
/// </summary>
public sealed class OAuthCallback(OAuthFlow flow, AccountService accounts, SessionTokenService tokens, AuthOptions options)
{
    [Function("OAuthCallback")]
    [AllowAnonymousAccess]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/callback/{provider}")] HttpRequestData req,
        string provider, FunctionContext context, CancellationToken ct)
    {
        var oauthProvider = flow.Resolve(provider); // unknown provider -> 404 from the JSON path, before we mark this a navigation
        ErrorRedirect.Set(context, "/login");

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
        ErrorRedirect.Set(context, failureTarget);

        var code = req.Query["code"];
        if (req.Query["error"] is not null || string.IsNullOrEmpty(code))
        {
            return Finish(OAuthFlow.Redirect(req, $"{failureTarget}?error=access_denied"));
        }

        var identity = await oauthProvider.ExchangeAsync(code, saved.CodeVerifier, options.RedirectUri(oauthProvider.Provider), ct);

        return saved.Mode == OAuthFlowMode.Link
            ? Finish(await CompleteLinkAsync(req, context, saved, identity, ct))
            : Finish(await CompleteLoginAsync(req, saved, identity, ct));
    }

    private async Task<HttpResponseData> CompleteLoginAsync(
        HttpRequestData req, OAuthState saved, ExternalIdentity identity, CancellationToken ct)
    {
        var user = await accounts.SignInAsync(identity, ct);

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

        await accounts.LinkAsync(currentUser, identity, ct);
        return OAuthFlow.Redirect(req, saved.ReturnUrl);
    }

    private static bool StateMatches(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
