using Microsoft.Azure.Functions.Worker;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Marks the current invocation as a browser navigation (the OAuth callback): a failure must send the browser
/// back into the SPA with `?error=`, not show JSON. <see cref="ExceptionHandlingMiddleware"/> reads the target
/// and produces the redirect, so the function itself needs no try/catch.
/// </summary>
public static class ErrorRedirect
{
    private const string ItemKey = "ephemeral-devpods.errorRedirectTarget";

    public static void Set(FunctionContext context, string target) => context.Items[ItemKey] = target;

    public static string? TryGet(FunctionContext context) =>
        context.Items.TryGetValue(ItemKey, out var value) ? value as string : null;
}
