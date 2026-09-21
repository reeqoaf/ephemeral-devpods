using Azure.Storage.Queues;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Infrastructure.Provisioning;

/// <summary>Azure Storage queue (Azurite locally) feeding the provisioning worker.</summary>
public sealed class StorageProvisioningQueue(QueueClient queue) : IProvisioningQueue
{
    public const string QueueName = "provision-environment";

    public async Task EnqueueAsync(ProvisionRequest request, CancellationToken ct) =>
        await queue.SendMessageAsync(request.ToMessage(), ct);
}
