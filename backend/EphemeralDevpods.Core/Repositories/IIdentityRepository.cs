using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Repositories;

public interface IIdentityRepository
{
    Task<LinkedIdentity?> FindAsync(IdentityProvider provider, string subject, CancellationToken ct);
    Task<IReadOnlyList<LinkedIdentity>> ListByUserAsync(string userId, CancellationToken ct);

    /// <summary>Inserts the identity; throws <see cref="IdentityAlreadyLinkedException"/> if that external identity already exists.</summary>
    Task AddAsync(LinkedIdentity identity, CancellationToken ct);

    Task DeleteAsync(IdentityProvider provider, string subject, CancellationToken ct);
}
