using EphemeralDevpods.Core.Auth;
using Microsoft.Azure.Functions.Worker;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// The authenticated user's id for the current invocation, placed there by <see cref="AuthMiddleware"/>
/// after validating the session cookie. There is deliberately no fallback identity: no session, no user.
/// </summary>
public static class CurrentUser
{
    private const string ItemKey = "ephemeral-devpods.userId";

    /// <summary>The user id; throws <see cref="UnauthorizedException"/> (401) if the request has no valid session.</summary>
    public static string GetId(FunctionContext context) =>
        TryGetId(context) ?? throw new UnauthorizedException();

    /// <summary>The user id, or null — for anonymous-allowed functions that behave differently when signed in.</summary>
    public static string? TryGetId(FunctionContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;

    internal static void Set(FunctionContext context, string userId) => context.Items[ItemKey] = userId;
}
