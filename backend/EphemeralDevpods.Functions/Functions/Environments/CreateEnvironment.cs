using System.Net;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>
/// Parses devcontainer.json synchronously and rejects unsupported repos with 400 before creating
/// any row (§14). Provisioning itself also runs synchronously for v1 — fine for local Docker's
/// pull/build times; likely needs to become fire-and-forget once ACI (slower) is wired in.
/// </summary>
public sealed class CreateEnvironment(
    RepoInspector inspector,
    HostPortSelector ports,
    IComputeProvisioner provisioner,
    IEnvironmentRepository environments,
    IResourceRepository resources,
    ILogger<CreateEnvironment> logger)
{
    [Function("CreateEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments")] HttpRequestData req,
        FunctionContext context, CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CreateEnvironmentRequest>(ct);
        if (string.IsNullOrWhiteSpace(body?.RepoUrl))
        {
            return await req.BadRequestAsync("repoUrl is required.", ct);
        }

        var inspection = await inspector.InspectAsync(body.RepoUrl, ct);
        var spec = inspection.Spec;
        var options = EnvironmentOptions.Resolve(
            body.Name, body.TtlMinutes, body.CpuCores, body.MemoryMb, body.TunnelProvider, inspection.SuggestedName);
        // Only local Docker publishes ports on the host; elsewhere there's nothing for the user to choose.
        var portMappings = provisioner.PublishesHostPorts
            ? await ports.ResolveAsync(spec.ForwardPorts, body.PortMappings, ct)
            : null;

        var environment = new WorkspaceEnvironment
        {
            Owner = CurrentUser.GetId(context),
            EnvironmentId = Guid.NewGuid().ToString(),
            RepoUrl = body.RepoUrl,
            Name = options.Name,
            Status = EnvironmentStatus.Provisioning,
            TtlMinutes = options.TtlMinutes,
            CreatedAt = DateTimeOffset.UtcNow,
            CpuCores = options.CpuCores,
            MemoryMb = options.MemoryMb,
            TunnelProvider = options.TunnelProvider,
            PortMappings = portMappings,
        };
        await environments.UpsertAsync(environment, ct);

        try
        {
            var result = await provisioner.ProvisionAsync(environment, spec, ct);
            environment.Status = EnvironmentStatus.Running;
            environment.PublicUrl = result.PublicUrl;
            environment.AccessToken = result.AccessToken;
            environment.TunnelName = result.TunnelName;

            foreach (var resource in result.Resources)
            {
                await resources.UpsertAsync(resource, ct);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Provisioning failed for environment {EnvironmentId}", environment.EnvironmentId);
            environment.Status = EnvironmentStatus.Failed;
        }

        await environments.UpsertAsync(environment, ct);

        return await req.WriteJsonAsync(HttpStatusCode.Created, EnvironmentResponse.From(environment), ct);
    }
}
