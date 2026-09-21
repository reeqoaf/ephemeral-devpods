using System.Net;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Auth;

/// <summary>Anonymous-allowed so a stale/expired session cookie can still be cleared.</summary>
public sealed class Logout(AuthOptions options)
{
    [Function("Logout")]
    [AllowAnonymousAccess]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "auth/logout")] HttpRequestData req)
    {
        var response = req.CreateResponse(HttpStatusCode.NoContent);
        response.ClearCookie(SessionTokenService.SessionCookie, "/", options.SecureCookies);
        return response;
    }
}
