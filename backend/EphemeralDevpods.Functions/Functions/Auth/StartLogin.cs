using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Auth;

/// <summary>Begins sign-in: redirects the browser to the provider's authorize endpoint.</summary>
public sealed class StartLogin(OAuthFlow flow)
{
    [Function("StartLogin")]
    [AllowAnonymousAccess]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/login/{provider}")] HttpRequestData req,
        string provider) =>
        flow.Start(req, provider, OAuthFlowMode.Login, userId: null, req.Query["returnUrl"]);
}
