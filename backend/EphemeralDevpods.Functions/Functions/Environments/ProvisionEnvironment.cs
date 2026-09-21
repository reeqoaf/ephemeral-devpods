using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning;
using Microsoft.Azure.Functions.Worker;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>Queue trigger that provisions environments accepted by <see cref="CreateEnvironment"/>.</summary>
public sealed class ProvisionEnvironment(EnvironmentProvisioning provisioning)
{
    [Function("ProvisionEnvironment")]
    public Task Run([QueueTrigger(StorageProvisioningQueue.QueueName)] string message, CancellationToken ct) =>
        provisioning.RunAsync(ProvisionRequest.FromMessage(message), ct);
}
