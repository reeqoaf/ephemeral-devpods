using System.Net;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

public sealed class ExtendEnvironment(IEnvironmentRepository environments)
{
    private const int ExtendByMinutes = 60;

    [Function("ExtendEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments/{environmentId}/extend")] HttpRequestData req,
        string environmentId, FunctionContext context, CancellationToken ct)
    {
        var owner = CurrentUser.GetId(context);
        var environment = await environments.GetAsync(owner, environmentId, ct); // §6: per-endpoint ownership check
        if (environment is null)
        {
            return await req.NotFoundAsync(ct);
        }

        environment.TtlMinutes += ExtendByMinutes;
        await environments.UpsertAsync(environment, ct);

        return await req.WriteJsonAsync(HttpStatusCode.OK, EnvironmentResponse.From(environment), ct);
    }
}
