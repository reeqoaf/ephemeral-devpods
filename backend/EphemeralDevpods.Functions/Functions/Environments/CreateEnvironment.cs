using System.Net;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>
/// Parses devcontainer.json synchronously and rejects unsupported repos with 400 before creating
/// any row (§14). Provisioning outlasts an HTTP request (image pulls, ACR builds), so the row is
/// created as Provisioning, the work is queued for <see cref="ProvisionEnvironment"/>, and the
/// call returns 202; the dashboard follows the status from there.
/// </summary>
public sealed class CreateEnvironment(
    RepoInspector inspector,
    HostPortSelector ports,
    IComputeProvisioner provisioner,
    IEnvironmentRepository environments,
    IProvisioningQueue provisioningQueue,
    ProvisioningGate gate)
{
    [Function("CreateEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments")] HttpRequestData req,
        FunctionContext context, CancellationToken ct)
    {
        await gate.EnsureAllowedAsync(CurrentUser.GetId(context), ct);

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
        await provisioningQueue.EnqueueAsync(new ProvisionRequest(environment.Owner, environment.EnvironmentId, spec), ct);

        return await req.WriteJsonAsync(HttpStatusCode.Accepted, EnvironmentResponse.From(environment), ct);
    }
}
