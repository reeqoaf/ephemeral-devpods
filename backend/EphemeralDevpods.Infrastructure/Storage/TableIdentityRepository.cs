using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>
/// Table Storage (or Azurite locally) backed implementation of the `identities` table. Keyed by
/// (provider, subject) so an external identity can only ever be linked to one user.
/// </summary>
public sealed class TableIdentityRepository(TableClient tableClient) : IIdentityRepository
{
    public async Task<LinkedIdentity?> FindAsync(IdentityProvider provider, string subject, CancellationToken ct)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<IdentityTableEntity>(provider.ToString(), subject, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // Cross-partition filter: fine for this table's size (a handful of rows per user), and only used
    // on account/link/unlink operations, never on the environment hot path.
    public async Task<IReadOnlyList<LinkedIdentity>> ListByUserAsync(string userId, CancellationToken ct)
    {
        var results = new List<LinkedIdentity>();
        await foreach (var entity in tableClient.QueryAsync<IdentityTableEntity>(e => e.UserId == userId, cancellationToken: ct))
        {
            results.Add(entity.ToDomain());
        }

        return results;
    }

    public async Task AddAsync(LinkedIdentity identity, CancellationToken ct)
    {
        try
        {
            await tableClient.AddEntityAsync(IdentityTableEntity.FromDomain(identity), ct);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            throw new IdentityAlreadyLinkedException(identity.Provider);
        }
    }

    public async Task DeleteAsync(IdentityProvider provider, string subject, CancellationToken ct)
    {
        await tableClient.DeleteEntityAsync(provider.ToString(), subject, cancellationToken: ct);
    }
}
