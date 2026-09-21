using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Core.Auth;

public sealed record Account(User User, IReadOnlyList<LinkedIdentity> Identities);

/// <summary>
/// Owns the sign-in / link / unlink rules. Accounts are only ever created via Microsoft; other providers
/// (GitHub) can sign in only once linked from an authenticated session.
/// </summary>
public sealed class AccountService(IUserRepository users, IIdentityRepository identities)
{
    public async Task<User> SignInAsync(ExternalIdentity external, CancellationToken ct)
    {
        var existing = await identities.FindAsync(external.Provider, external.Subject, ct);
        if (existing is not null)
        {
            return await users.GetAsync(existing.UserId, ct) ?? throw new UnauthorizedException("Account no longer exists.");
        }

        if (external.Provider != IdentityProvider.Microsoft)
        {
            throw new IdentityNotLinkedException(external.Provider);
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            DisplayName = external.DisplayName,
            Email = external.Email,
            CreatedAt = now,
        };

        try
        {
            // Identity first: it's the uniqueness gate, so a concurrent first sign-in can't leave an orphan user.
            await identities.AddAsync(ToLinked(user.UserId, external, now), ct);
        }
        catch (IdentityAlreadyLinkedException)
        {
            var winner = await identities.FindAsync(external.Provider, external.Subject, ct)
                ?? throw new UnauthorizedException("Account no longer exists.");
            return await users.GetAsync(winner.UserId, ct) ?? throw new UnauthorizedException("Account no longer exists.");
        }

        await users.AddAsync(user, ct);
        return user;
    }

    public async Task LinkAsync(string userId, ExternalIdentity external, CancellationToken ct)
    {
        var existing = await identities.FindAsync(external.Provider, external.Subject, ct);
        if (existing is not null)
        {
            if (existing.UserId == userId)
            {
                return;
            }

            throw new IdentityAlreadyLinkedException(external.Provider);
        }

        var mine = await identities.ListByUserAsync(userId, ct);
        if (mine.Any(i => i.Provider == external.Provider))
        {
            throw new AccountRuleException($"A {external.Provider} account is already linked. Unlink it first.");
        }

        await identities.AddAsync(ToLinked(userId, external, DateTimeOffset.UtcNow), ct);
    }

    public async Task UnlinkAsync(string userId, IdentityProvider provider, CancellationToken ct)
    {
        if (provider == IdentityProvider.Microsoft)
        {
            throw new AccountRuleException("The Microsoft account is the root of your account and can't be unlinked.");
        }

        var target = (await identities.ListByUserAsync(userId, ct)).FirstOrDefault(i => i.Provider == provider)
            ?? throw new KeyNotFoundException();
        await identities.DeleteAsync(target.Provider, target.Subject, ct);
    }

    public async Task<Account> GetAccountAsync(string userId, CancellationToken ct)
    {
        var user = await users.GetAsync(userId, ct) ?? throw new UnauthorizedException("Account no longer exists.");
        return new Account(user, await identities.ListByUserAsync(userId, ct));
    }

    private static LinkedIdentity ToLinked(string userId, ExternalIdentity external, DateTimeOffset now) => new()
    {
        Provider = external.Provider,
        Subject = external.Subject,
        UserId = userId,
        DisplayName = external.DisplayName,
        LinkedAt = now,
    };
}
