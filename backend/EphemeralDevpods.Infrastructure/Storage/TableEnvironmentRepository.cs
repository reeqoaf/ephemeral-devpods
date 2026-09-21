using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage (or Azurite locally) backed implementation of the `environments` table.</summary>
public sealed class TableEnvironmentRepository(TableClient tableClient) : IEnvironmentRepository
{
    public async Task<WorkspaceEnvironment?> GetAsync(string owner, string environmentId, CancellationToken ct)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<EnvironmentTableEntity>(owner, environmentId, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task<IReadOnlyList<WorkspaceEnvironment>> ListByOwnerAsync(string owner, CancellationToken ct)
    {
        var results = new List<WorkspaceEnvironment>();
        await foreach (var entity in tableClient.QueryAsync<EnvironmentTableEntity>(
            e => e.PartitionKey == owner, cancellationToken: ct))
        {
            results.Add(entity.ToDomain());
        }

        return results;
    }

    public async Task<IReadOnlyList<WorkspaceEnvironment>> ListExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken ct)
    {
        var results = new List<WorkspaceEnvironment>();
        await foreach (var entity in tableClient.QueryAsync<EnvironmentTableEntity>(
            e => e.Status == nameof(EnvironmentStatus.Running) ||
                 e.Status == nameof(EnvironmentStatus.Provisioning) ||
                 e.Status == nameof(EnvironmentStatus.Stopped),
            cancellationToken: ct))
        {
            if (entity.CreatedAt.AddMinutes(entity.TtlMinutes) <= cutoff)
            {
                results.Add(entity.ToDomain());
            }
        }

        return results;
    }

    public async Task<IReadOnlyList<WorkspaceEnvironment>> ListActiveAsync(CancellationToken ct)
    {
        var results = new List<WorkspaceEnvironment>();
        await foreach (var entity in tableClient.QueryAsync<EnvironmentTableEntity>(
            e => e.Status != nameof(EnvironmentStatus.Expired), cancellationToken: ct))
        {
            results.Add(entity.ToDomain());
        }

        return results;
    }

    public async Task UpsertAsync(WorkspaceEnvironment environment, CancellationToken ct)
    {
        await tableClient.UpsertEntityAsync(EnvironmentTableEntity.FromDomain(environment), TableUpdateMode.Replace, ct);
    }
}
