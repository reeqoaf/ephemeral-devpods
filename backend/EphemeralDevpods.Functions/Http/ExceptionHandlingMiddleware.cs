using System.Net;
using System.Text.Json;
using EphemeralDevpods.Core;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Functions.Functions.Auth;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Single place that turns exceptions from HTTP-triggered functions into JSON error responses, so
/// individual functions don't need try/catch just to map failures to status codes.
/// Functions that are browser navigations (see <see cref="ErrorRedirect"/>) get a redirect instead of JSON.
/// Non-HTTP triggers (timers) are logged and rethrown.
/// </summary>
public sealed class ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger, AuthOptions authOptions)
    : IFunctionsWorkerMiddleware
{
    public const string ProviderErrorCode = "provider_error";

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            var req = await context.GetHttpRequestDataAsync();
            if (req is null)
            {
                logger.LogError(ex, "Unhandled exception in {FunctionName}", context.FunctionDefinition.Name);
                throw;
            }

            if (ErrorRedirect.TryGet(context) is { } target)
            {
                context.GetInvocationResult().Value = RedirectWithError(req, target, ex, context.FunctionDefinition.Name);
                return;
            }

            var (status, message) = ex switch
            {
                UnauthorizedException => (HttpStatusCode.Unauthorized, "Authentication required."),
                ConflictException => (HttpStatusCode.Conflict, ex.Message),
                UserInputException => (HttpStatusCode.BadRequest, ex.Message),
                JsonException => (HttpStatusCode.BadRequest, "Request body is not valid JSON."),
                KeyNotFoundException => (HttpStatusCode.NotFound, "Not found."),
                _ => (HttpStatusCode.InternalServerError, "Internal server error."),
            };

            if (status == HttpStatusCode.InternalServerError)
            {
                logger.LogError(ex, "Unhandled exception in {FunctionName}", context.FunctionDefinition.Name);
            }

            var response = await req.WriteJsonAsync(status, new { error = message }, context.CancellationToken);
            context.GetInvocationResult().Value = response;
        }
    }

    /// <summary>The `?error=` code the SPA understands for failures the user can act on; null for anything else.</summary>
    public static string? ExpectedAuthErrorCode(Exception ex) => ex switch
    {
        IdentityNotLinkedException e => $"{e.Provider.ToString().ToLowerInvariant()}_not_linked",
        IdentityAlreadyLinkedException => "already_linked",
        AccountRuleException => "provider_already_linked",
        _ => null,
    };

    private HttpResponseData RedirectWithError(HttpRequestData req, string target, Exception ex, string functionName)
    {
        var code = ExpectedAuthErrorCode(ex);
        if (code is null)
        {
            // Not something the user can act on: the provider (or our call to it) failed.
            if (ex is OAuthExchangeException)
            {
                logger.LogWarning(ex, "OAuth exchange failed in {FunctionName}", functionName);
            }
            else
            {
                logger.LogError(ex, "Unhandled exception in {FunctionName}", functionName);
            }

            code = ProviderErrorCode;
        }

        var response = OAuthFlow.Redirect(req, $"{target}?error={code}");
        response.ClearCookie(SessionTokenService.StateCookie, OAuthFlow.StateCookiePath, authOptions.SecureCookies);
        return response;
    }
}
