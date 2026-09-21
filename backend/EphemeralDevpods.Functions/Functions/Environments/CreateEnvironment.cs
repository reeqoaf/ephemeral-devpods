using System.Net;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Parsing;
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
    IDevcontainerFileFetcher fileFetcher,
    IDevcontainerParser parser,
    IComputeProvisioner provisioner,
    IEnvironmentRepository environments,
    IResourceRepository resources,
    ILogger<CreateEnvironment> logger)
{
    private const int DefaultTtlMinutes = 60;

    [Function("CreateEnvironment")]
    public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "environments")] HttpRequestData req,
        CancellationToken ct)
    {
        var body = await req.ReadJsonAsync<CreateEnvironmentRequest>(ct);
        if (string.IsNullOrWhiteSpace(body?.RepoUrl))
        {
            return await req.BadRequestAsync("repoUrl is required.", ct);
        }

        var file = await fileFetcher.FetchAsync(body.RepoUrl, ct);

        if (file is null)
        {
            return await req.BadRequestAsync(
                "No devcontainer.json found (checked .devcontainer/devcontainer.json and .devcontainer.json).", ct);
        }

        var spec = parser.Parse(file.Content, file.BaseDirectory);

        var environment = new WorkspaceEnvironment
        {
            Owner = CurrentUser.GetId(req),
            EnvironmentId = Guid.NewGuid().ToString(),
            RepoUrl = body.RepoUrl,
            Status = EnvironmentStatus.Provisioning,
            TtlMinutes = DefaultTtlMinutes,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        await environments.UpsertAsync(environment, ct);

        try
        {
            var result = await provisioner.ProvisionAsync(environment, spec, ct);
            environment.Status = EnvironmentStatus.Running;
            environment.PublicUrl = result.PublicUrl;
            environment.AccessToken = result.AccessToken;

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
