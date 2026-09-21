using System.Net;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Functions.Functions.Auth;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Me;

public sealed class UnlinkIdentity(AccountService accounts)
{
    [Function("UnlinkIdentity")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "me/identities/{provider}")] HttpRequestData req,
        string provider, FunctionContext context, CancellationToken ct)
    {
        await accounts.UnlinkAsync(CurrentUser.GetId(context), OAuthFlow.ParseProvider(provider), ct);
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
