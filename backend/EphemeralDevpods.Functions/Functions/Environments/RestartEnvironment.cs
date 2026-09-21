using System.Net;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>Restarts a running environment's container.</summary>
public sealed class RestartEnvironment(
    IEnvironmentRepository environments, EnvironmentLifecycle lifecycle, EnvironmentStatusSync statusSync)
{
    [Function("RestartEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments/{environmentId}/restart")] HttpRequestData req,
        string environmentId, FunctionContext context, CancellationToken ct)
    {
        var environment = await environments.GetAsync(CurrentUser.GetId(context), environmentId, ct); // §6: ownership check
        if (environment is null)
        {
            return await req.NotFoundAsync(ct);
        }

        await lifecycle.RestartAsync(environment, ct);

        var snapshot = await statusSync.RefreshAsync(environment, ct);
        return await req.WriteJsonAsync(
            HttpStatusCode.OK, EnvironmentResponse.From(snapshot.Environment, snapshot.Tunnel), ct);
    }
}
