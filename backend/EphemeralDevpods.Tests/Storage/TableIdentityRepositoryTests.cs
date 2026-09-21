using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Storage;

namespace EphemeralDevpods.Tests.Storage;

public class TableIdentityRepositoryTests(AzuriteFixture fixture) : IClassFixture<AzuriteFixture>
{
    private readonly TableIdentityRepository _identities = new(fixture.IdentitiesTable);
    private readonly TableUserRepository _users = new(fixture.UsersTable);

    private static LinkedIdentity MakeIdentity(string userId, IdentityProvider provider, string? subject = null) => new()
    {
        Provider = provider,
        Subject = subject ?? Guid.NewGuid().ToString("N"),
        UserId = userId,
        DisplayName = "someone",
        LinkedAt = DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Add_then_find_round_trips()
    {
        var identity = MakeIdentity("u1", IdentityProvider.GitHub);

        await _identities.AddAsync(identity, CancellationToken.None);
        var loaded = await _identities.FindAsync(IdentityProvider.GitHub, identity.Subject, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("u1", loaded.UserId);
        Assert.Equal(IdentityProvider.GitHub, loaded.Provider);
    }

    [Fact]
    public async Task Find_returns_null_when_missing()
    {
        Assert.Null(await _identities.FindAsync(IdentityProvider.Microsoft, "nope", CancellationToken.None));
    }

    [Fact]
    public async Task Same_subject_under_a_different_provider_is_a_different_identity()
    {
        var subject = Guid.NewGuid().ToString("N");
        await _identities.AddAsync(MakeIdentity("u1", IdentityProvider.Microsoft, subject), CancellationToken.None);

        Assert.Null(await _identities.FindAsync(IdentityProvider.GitHub, subject, CancellationToken.None));
    }

    [Fact]
    public async Task Adding_an_existing_external_identity_throws_even_for_a_different_user()
    {
        var first = MakeIdentity("u1", IdentityProvider.GitHub);
        await _identities.AddAsync(first, CancellationToken.None);

        await Assert.ThrowsAsync<IdentityAlreadyLinkedException>(() =>
            _identities.AddAsync(MakeIdentity("u2", IdentityProvider.GitHub, first.Subject), CancellationToken.None));

        var stillOwnedBy = await _identities.FindAsync(IdentityProvider.GitHub, first.Subject, CancellationToken.None);
        Assert.Equal("u1", stillOwnedBy!.UserId);
    }

    [Fact]
    public async Task ListByUser_returns_only_that_users_identities()
    {
        var user = Guid.NewGuid().ToString("N");
        await _identities.AddAsync(MakeIdentity(user, IdentityProvider.Microsoft), CancellationToken.None);
        await _identities.AddAsync(MakeIdentity(user, IdentityProvider.GitHub), CancellationToken.None);
        await _identities.AddAsync(MakeIdentity(Guid.NewGuid().ToString("N"), IdentityProvider.GitHub), CancellationToken.None);

        var results = await _identities.ListByUserAsync(user, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(user, r.UserId));
    }

    [Fact]
    public async Task Delete_removes_the_identity()
    {
        var identity = MakeIdentity("u1", IdentityProvider.GitHub);
        await _identities.AddAsync(identity, CancellationToken.None);

        await _identities.DeleteAsync(identity.Provider, identity.Subject, CancellationToken.None);

        Assert.Null(await _identities.FindAsync(identity.Provider, identity.Subject, CancellationToken.None));
    }

    [Fact]
    public async Task User_add_then_get_round_trips_and_missing_is_null()
    {
        var user = new User
        {
            UserId = Guid.NewGuid().ToString("N"),
            DisplayName = "Alice",
            Email = "alice@example.com",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        await _users.AddAsync(user, CancellationToken.None);
        var loaded = await _users.GetAsync(user.UserId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("Alice", loaded.DisplayName);
        Assert.Equal("alice@example.com", loaded.Email);
        Assert.Null(await _users.GetAsync("missing", CancellationToken.None));
    }
}
