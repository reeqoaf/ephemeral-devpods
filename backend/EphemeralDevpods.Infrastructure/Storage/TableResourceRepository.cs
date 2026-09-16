using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage (or Azurite locally) backed implementation of the `resources` table.</summary>
public sealed class TableResourceRepository(TableClient tableClient) : IResourceRepository
{
    public async Task<IReadOnlyList<DeployedResource>> ListByEnvironmentAsync(string environmentId, CancellationToken ct)
    {
        var results = new List<DeployedResource>();
        await foreach (var entity in tableClient.QueryAsync<ResourceTableEntity>(
            e => e.PartitionKey == environmentId, cancellationToken: ct))
        {
            results.Add(entity.ToDomain());
        }

        return results;
    }

    public async Task UpsertAsync(DeployedResource resource, CancellationToken ct)
    {
        await tableClient.UpsertEntityAsync(ResourceTableEntity.FromDomain(resource), TableUpdateMode.Replace, ct);
    }

    public async Task DeleteAsync(string environmentId, string resourceId, CancellationToken ct)
    {
        await tableClient.DeleteEntityAsync(environmentId, resourceId, cancellationToken: ct);
    }
}
