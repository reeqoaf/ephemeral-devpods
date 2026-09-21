using System.Collections.Concurrent;
using System.Reflection;
using EphemeralDevpods.Core.Auth;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Default-deny authentication for HTTP-triggered functions. Validates the `session` cookie and exposes the
/// user via <see cref="CurrentUser"/>. A request without a valid session is rejected with 401 unless the
/// function is marked <see cref="AllowAnonymousAccessAttribute"/>. Runs inside <see cref="ExceptionHandlingMiddleware"/>,
/// which turns the <see cref="UnauthorizedException"/> into the response.
/// </summary>
public sealed class AuthMiddleware(SessionTokenService tokens) : IFunctionsWorkerMiddleware
{
    private static readonly ConcurrentDictionary<string, bool> AnonymousByEntryPoint = new();

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var req = await context.GetHttpRequestDataAsync();
        if (req is null)
        {
            // Non-HTTP trigger (timer): no user context by design.
            await next(context);
            return;
        }

        var cookie = req.GetCookie(SessionTokenService.SessionCookie);
        var userId = cookie is null ? null : await tokens.ValidateSessionAsync(cookie);
        if (userId is not null)
        {
            CurrentUser.Set(context, userId);
        }
        else if (!IsAnonymousAllowed(context.FunctionDefinition.EntryPoint))
        {
            throw new UnauthorizedException();
        }

        await next(context);
    }

    /// <summary>Unknown or unresolvable entry points are treated as requiring auth.</summary>
    public static bool IsAnonymousAllowed(string entryPoint) =>
        AnonymousByEntryPoint.GetOrAdd(entryPoint, static ep =>
        {
            var split = ep.LastIndexOf('.');
            if (split < 0)
            {
                return false;
            }

            var type = typeof(AuthMiddleware).Assembly.GetType(ep[..split]);
            var method = type?.GetMethod(ep[(split + 1)..], BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
            return method?.GetCustomAttribute<AllowAnonymousAccessAttribute>() is not null;
        });
}
