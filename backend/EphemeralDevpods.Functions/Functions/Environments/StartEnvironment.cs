using System.Net;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>Starts a stopped (or crashed) environment's container again.</summary>
public sealed class StartEnvironment(
    IEnvironmentRepository environments, EnvironmentLifecycle lifecycle, EnvironmentStatusSync statusSync,
    ProvisioningGate gate)
{
    [Function("StartEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments/{environmentId}/start")] HttpRequestData req,
        string environmentId, FunctionContext context, CancellationToken ct)
    {
        var owner = CurrentUser.GetId(context);
        await gate.EnsureAllowedAsync(owner, ct);

        var environment = await environments.GetAsync(owner, environmentId, ct); // §6: ownership check
        if (environment is null)
        {
            return await req.NotFoundAsync(ct);
        }

        await lifecycle.StartAsync(environment, ct);

        var snapshot = await statusSync.RefreshAsync(environment, ct);
        return await req.WriteJsonAsync(
            HttpStatusCode.OK, EnvironmentResponse.From(snapshot.Environment, snapshot.Tunnel), ct);
    }
}
