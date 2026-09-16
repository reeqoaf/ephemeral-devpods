using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Repositories;

public interface IResourceRepository
{
    Task<IReadOnlyList<DeployedResource>> ListByEnvironmentAsync(string environmentId, CancellationToken ct);

    Task UpsertAsync(DeployedResource resource, CancellationToken ct);

    Task DeleteAsync(string environmentId, string resourceId, CancellationToken ct);
}
