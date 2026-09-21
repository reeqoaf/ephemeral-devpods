using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage (or Azurite locally) backed implementation of the `users` table.</summary>
public sealed class TableUserRepository(TableClient tableClient) : IUserRepository
{
    public async Task<User?> GetAsync(string userId, CancellationToken ct)
    {
        try
        {
            var response = await tableClient.GetEntityAsync<UserTableEntity>(UserTableEntity.Partition, userId, cancellationToken: ct);
            return response.Value.ToDomain();
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task AddAsync(User user, CancellationToken ct)
    {
        await tableClient.AddEntityAsync(UserTableEntity.FromDomain(user), ct);
    }
}
