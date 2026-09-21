using System.Net;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>Tears an environment down now instead of waiting for TTL expiry (the dashboard's Stop button).</summary>
public sealed class DeleteEnvironment(
    IEnvironmentRepository environments, IResourceRepository resources, IComputeProvisioner provisioner)
{
    [Function("DeleteEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "environments/{environmentId}")] HttpRequestData req,
        string environmentId, FunctionContext context, CancellationToken ct)
    {
        var owner = CurrentUser.GetId(context);
        var environment = await environments.GetAsync(owner, environmentId, ct); // §6: per-endpoint ownership check
        if (environment is null)
        {
            return await req.NotFoundAsync(ct);
        }

        await provisioner.TeardownAsync(environmentId, ct);

        foreach (var resource in await resources.ListByEnvironmentAsync(environmentId, ct))
        {
            await resources.DeleteAsync(environmentId, resource.ResourceId, ct);
        }

        // Row is kept, not deleted — it doubles as history/audit log (§7/§9).
        environment.Status = EnvironmentStatus.Expired;
        await environments.UpsertAsync(environment, ct);

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}
