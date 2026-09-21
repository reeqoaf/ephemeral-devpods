using System.Net;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>Step 1 of the create flow: checks a repo would work, without creating anything.</summary>
public sealed class CheckRepository(RepoInspector inspector, IComputeProvisioner provisioner)
{
    [Function("CheckRepository")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments/check")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CheckRepositoryRequest>(ct);
        if (string.IsNullOrWhiteSpace(body?.RepoUrl))
        {
            return await req.BadRequestAsync("repoUrl is required.", ct);
        }

        var inspection = await inspector.InspectAsync(body.RepoUrl, ct);
        var portMappings = PortMapping.Identity(inspection.Spec.ForwardPorts);
        var selectable = provisioner.PublishesHostPorts && portMappings.Count > 0;

        return await req.WriteJsonAsync(
            HttpStatusCode.OK, CheckRepositoryResponse.From(body.RepoUrl, inspection, portMappings, selectable), ct);
    }
}
