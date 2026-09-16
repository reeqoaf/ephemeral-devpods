using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace EphemeralDevpods.Functions.Functions.Teardown;

/// <summary>Sweeps environments past their TTL and tears down their tagged resources.</summary>
public sealed class TeardownTimer(
    IEnvironmentRepository environments, IResourceRepository resources, IComputeProvisioner provisioner,
    ILogger<TeardownTimer> logger)
{
    [Function("TeardownTimer")]
    public async Task Run([TimerTrigger("0 */5 * * * *")] TimerInfo timer, CancellationToken ct)
    {
        var expiring = await environments.ListExpiringBeforeAsync(DateTimeOffset.UtcNow, ct);

        foreach (var environment in expiring)
        {
            try
            {
                await provisioner.TeardownAsync(environment.EnvironmentId, ct);

                foreach (var resource in await resources.ListByEnvironmentAsync(environment.EnvironmentId, ct))
                {
                    await resources.DeleteAsync(environment.EnvironmentId, resource.ResourceId, ct);
                }

                environment.Status = EnvironmentStatus.Expired;
                await environments.UpsertAsync(environment, ct);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to tear down expired environment {EnvironmentId}", environment.EnvironmentId);
            }
        }
    }
}
