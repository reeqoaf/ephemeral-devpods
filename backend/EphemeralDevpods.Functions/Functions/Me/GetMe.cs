using System.Net;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Me;

public sealed record LinkedIdentityResponse(string Provider, string DisplayName, DateTimeOffset LinkedAt);

public sealed record MeResponse(string UserId, string DisplayName, string? Email, IReadOnlyList<LinkedIdentityResponse> Identities);

/// <summary>The signed-in user's profile and linked identities. The SPA uses a 401 here to mean "signed out".</summary>
public sealed class GetMe(AccountService accounts)
{
    [Function("GetMe")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequestData req,
        FunctionContext context, CancellationToken ct)
    {
        var account = await accounts.GetAccountAsync(CurrentUser.GetId(context), ct);
        var identities = account.Identities
            .Select(i => new LinkedIdentityResponse(i.Provider.ToString(), i.DisplayName, i.LinkedAt))
            .ToList();
        return await req.WriteJsonAsync(
            HttpStatusCode.OK,
            new MeResponse(account.User.UserId, account.User.DisplayName, account.User.Email, identities),
            ct);
    }
}
