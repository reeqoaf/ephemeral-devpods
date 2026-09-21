using System.Net;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

public sealed class GetEnvironment(IEnvironmentRepository environments, EnvironmentStatusSync statusSync)
{
    [Function("GetEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "environments/{environmentId}")] HttpRequestData req,
        string environmentId, FunctionContext context, CancellationToken ct)
    {
        var owner = CurrentUser.GetId(context);
        // §6: independently verify ownership here, not just at ListEnvironments — otherwise a
        // guessed/enumerated environmentId belonging to another user would leak through.
        var environment = await environments.GetAsync(owner, environmentId, ct);
        if (environment is null)
        {
            return await req.NotFoundAsync(ct);
        }

        environment = await statusSync.RefreshAsync(environment, ct);
        return await req.WriteJsonAsync(HttpStatusCode.OK, EnvironmentResponse.From(environment), ct);
    }
}
