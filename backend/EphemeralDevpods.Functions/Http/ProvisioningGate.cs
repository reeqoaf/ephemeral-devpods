using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Infrastructure.Provisioning;

namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// The abuse control for the cloud backend: sign-up is open to any Microsoft account and every environment costs
/// money, so only admins may run compute there. Local Docker costs nothing, so the gate is open in that mode.
/// Guards everything that consumes compute (create, start, restart, extend); stop, delete and reads stay open so
/// a user who lost access can still shut their environments down.
/// </summary>
public sealed class ProvisioningGate(IUserRepository users, ComputeOptions compute)
{
    public bool IsAllowed(User user) => compute.Provider != ComputeProvider.Aci || user.IsAdmin;

    /// <exception cref="ForbiddenException">The user isn't allowed to run environments.</exception>
    /// <exception cref="UnauthorizedException">The user's account no longer exists.</exception>
    public async Task EnsureAllowedAsync(string userId, CancellationToken ct)
    {
        if (compute.Provider != ComputeProvider.Aci)
        {
            return; // nothing to read: the gate is open, so don't spend a table lookup on every call
        }

        var user = await users.GetAsync(userId, ct) ?? throw new UnauthorizedException("Account no longer exists.");
        if (!IsAllowed(user))
        {
            throw new ForbiddenException("Admin access is required to run environments.");
        }
    }
}
