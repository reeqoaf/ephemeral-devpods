using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Resolves the authenticated user. In production, Easy Auth injects x-ms-client-principal-name
/// on every request. Local dev has no Easy Auth to exercise (§6/§13), so it falls back to a fixed
/// fake user — matching the documented local-dev decision.
/// </summary>
public static class CurrentUser
{
    private const string LocalDevUserId = "local-dev-user";
    private const string EasyAuthPrincipalNameHeader = "x-ms-client-principal-name";

    public static string GetId(HttpRequestData req)
    {
        if (req.Headers.TryGetValues(EasyAuthPrincipalNameHeader, out var values))
        {
            var name = values.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return LocalDevUserId;
    }
}
