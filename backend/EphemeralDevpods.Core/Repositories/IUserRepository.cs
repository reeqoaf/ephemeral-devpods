using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Repositories;

public interface IUserRepository
{
    Task<User?> GetAsync(string userId, CancellationToken ct);
    Task AddAsync(User user, CancellationToken ct);
}
