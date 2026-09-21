using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Auth;

/// <summary>
/// Begins linking another provider to the signed-in user. Requires a session (default-deny applies), and
/// binds that user's id into the state cookie so the callback can't attach the identity to anyone else.
/// </summary>
public sealed class StartLink(OAuthFlow flow)
{
    [Function("StartLink")]
    public HttpResponseData Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "auth/link/{provider}")] HttpRequestData req,
        string provider, FunctionContext context) =>
        flow.Start(req, provider, OAuthFlowMode.Link, CurrentUser.GetId(context), req.Query["returnUrl"]);
}
