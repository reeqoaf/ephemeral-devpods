using System.Net;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

public sealed class ListEnvironments(IEnvironmentRepository environments, EnvironmentStatusSync statusSync)
{
    [Function("ListEnvironments")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "environments")] HttpRequestData req,
        FunctionContext context, CancellationToken ct)
    {
        var owner = CurrentUser.GetId(context);
        var results = await environments.ListByOwnerAsync(owner, ct);

        var refreshed = new List<EnvironmentResponse>(results.Count);
        foreach (var environment in results)
        {
            var snapshot = await statusSync.RefreshAsync(environment, ct);
            refreshed.Add(EnvironmentResponse.From(snapshot.Environment, snapshot.Tunnel));
        }

        return await req.WriteJsonAsync(HttpStatusCode.OK, refreshed, ct);
    }
}
