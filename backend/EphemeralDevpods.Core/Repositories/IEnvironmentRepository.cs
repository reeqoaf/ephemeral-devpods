using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Repositories;

public interface IEnvironmentRepository
{
    Task<WorkspaceEnvironment?> GetAsync(string owner, string environmentId, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceEnvironment>> ListByOwnerAsync(string owner, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceEnvironment>> ListExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);

    Task UpsertAsync(WorkspaceEnvironment environment, CancellationToken ct);
}
