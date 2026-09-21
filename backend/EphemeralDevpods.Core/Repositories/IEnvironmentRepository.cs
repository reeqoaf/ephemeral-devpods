using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Repositories;

public interface IEnvironmentRepository
{
    Task<WorkspaceEnvironment?> GetAsync(string owner, string environmentId, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceEnvironment>> ListByOwnerAsync(string owner, CancellationToken ct);

    Task<IReadOnlyList<WorkspaceEnvironment>> ListExpiringBeforeAsync(DateTimeOffset cutoff, CancellationToken ct);

    /// <summary>Every owner's environments that haven't been torn down (anything but Expired).</summary>
    Task<IReadOnlyList<WorkspaceEnvironment>> ListActiveAsync(CancellationToken ct);

    Task UpsertAsync(WorkspaceEnvironment environment, CancellationToken ct);
}
