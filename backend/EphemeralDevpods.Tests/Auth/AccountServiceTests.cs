using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Tests.Auth;

public class AccountServiceTests
{
    private sealed class FakeUsers : IUserRepository
    {
        public Dictionary<string, User> Rows { get; } = [];
        public Task<User?> GetAsync(string userId, CancellationToken ct) => Task.FromResult(Rows.GetValueOrDefault(userId));
        public Task AddAsync(User user, CancellationToken ct) { Rows[user.UserId] = user; return Task.CompletedTask; }
    }

    private sealed class FakeIdentities : IIdentityRepository
    {
        public Dictionary<(IdentityProvider, string), LinkedIdentity> Rows { get; } = [];

        public Task<LinkedIdentity?> FindAsync(IdentityProvider provider, string subject, CancellationToken ct) =>
            Task.FromResult(Rows.GetValueOrDefault((provider, subject)));

        public Task<IReadOnlyList<LinkedIdentity>> ListByUserAsync(string userId, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<LinkedIdentity>>(Rows.Values.Where(i => i.UserId == userId).ToList());

        public Task AddAsync(LinkedIdentity identity, CancellationToken ct)
        {
            if (!Rows.TryAdd((identity.Provider, identity.Subject), identity))
            {
                throw new IdentityAlreadyLinkedException(identity.Provider);
            }

            return Task.CompletedTask;
        }

        public Task DeleteAsync(IdentityProvider provider, string subject, CancellationToken ct)
        {
            Rows.Remove((provider, subject));
            return Task.CompletedTask;
        }
    }

    private readonly FakeUsers _users = new();
    private readonly FakeIdentities _identities = new();
    private readonly AccountService _service;

    public AccountServiceTests() => _service = new AccountService(_users, _identities);

    private static ExternalIdentity Microsoft(string subject = "ms-1", string name = "Alice") =>
        new(IdentityProvider.Microsoft, subject, name, $"{name.ToLowerInvariant()}@example.com");

    private static ExternalIdentity GitHub(string subject = "gh-1", string login = "alice-gh") =>
        new(IdentityProvider.GitHub, subject, login, null);

    [Fact]
    public async Task First_microsoft_sign_in_creates_a_user_and_links_the_identity()
    {
        var user = await _service.SignInAsync(Microsoft(), CancellationToken.None);

        Assert.Equal("Alice", user.DisplayName);
        Assert.Single(_users.Rows);
        Assert.Equal(user.UserId, _identities.Rows[(IdentityProvider.Microsoft, "ms-1")].UserId);
    }

    [Fact]
    public async Task Repeat_microsoft_sign_in_returns_the_same_user()
    {
        var first = await _service.SignInAsync(Microsoft(), CancellationToken.None);
        var second = await _service.SignInAsync(Microsoft(), CancellationToken.None);

        Assert.Equal(first.UserId, second.UserId);
        Assert.Single(_users.Rows);
    }

    [Fact]
    public async Task Different_microsoft_accounts_get_different_users()
    {
        var alice = await _service.SignInAsync(Microsoft("ms-1", "Alice"), CancellationToken.None);
        var bob = await _service.SignInAsync(Microsoft("ms-2", "Bob"), CancellationToken.None);

        Assert.NotEqual(alice.UserId, bob.UserId);
    }

    [Fact]
    public async Task Unlinked_github_sign_in_is_rejected_and_creates_nothing()
    {
        await Assert.ThrowsAsync<IdentityNotLinkedException>(() => _service.SignInAsync(GitHub(), CancellationToken.None));

        Assert.Empty(_users.Rows);
        Assert.Empty(_identities.Rows);
    }

    [Fact]
    public async Task Linked_github_signs_in_as_the_same_user_as_microsoft()
    {
        var viaMicrosoft = await _service.SignInAsync(Microsoft(), CancellationToken.None);
        await _service.LinkAsync(viaMicrosoft.UserId, GitHub(), CancellationToken.None);

        var viaGitHub = await _service.SignInAsync(GitHub(), CancellationToken.None);

        Assert.Equal(viaMicrosoft.UserId, viaGitHub.UserId);
        Assert.Single(_users.Rows);
    }

    [Fact]
    public async Task Linking_an_identity_owned_by_another_user_conflicts()
    {
        var alice = await _service.SignInAsync(Microsoft("ms-1", "Alice"), CancellationToken.None);
        var bob = await _service.SignInAsync(Microsoft("ms-2", "Bob"), CancellationToken.None);
        await _service.LinkAsync(alice.UserId, GitHub(), CancellationToken.None);

        await Assert.ThrowsAsync<IdentityAlreadyLinkedException>(() =>
            _service.LinkAsync(bob.UserId, GitHub(), CancellationToken.None));

        Assert.Equal(alice.UserId, _identities.Rows[(IdentityProvider.GitHub, "gh-1")].UserId);
    }

    [Fact]
    public async Task Linking_the_same_identity_twice_to_the_same_user_is_a_no_op()
    {
        var alice = await _service.SignInAsync(Microsoft(), CancellationToken.None);
        await _service.LinkAsync(alice.UserId, GitHub(), CancellationToken.None);

        await _service.LinkAsync(alice.UserId, GitHub(), CancellationToken.None);

        Assert.Equal(2, (await _service.GetAccountAsync(alice.UserId, CancellationToken.None)).Identities.Count);
    }

    [Fact]
    public async Task A_second_github_account_cannot_be_linked_until_the_first_is_unlinked()
    {
        var alice = await _service.SignInAsync(Microsoft(), CancellationToken.None);
        await _service.LinkAsync(alice.UserId, GitHub("gh-1"), CancellationToken.None);

        await Assert.ThrowsAsync<AccountRuleException>(() =>
            _service.LinkAsync(alice.UserId, GitHub("gh-2"), CancellationToken.None));

        await _service.UnlinkAsync(alice.UserId, IdentityProvider.GitHub, CancellationToken.None);
        await _service.LinkAsync(alice.UserId, GitHub("gh-2"), CancellationToken.None);
    }

    [Fact]
    public async Task Unlinked_github_can_no_longer_sign_in()
    {
        var alice = await _service.SignInAsync(Microsoft(), CancellationToken.None);
        await _service.LinkAsync(alice.UserId, GitHub(), CancellationToken.None);

        await _service.UnlinkAsync(alice.UserId, IdentityProvider.GitHub, CancellationToken.None);

        await Assert.ThrowsAsync<IdentityNotLinkedException>(() => _service.SignInAsync(GitHub(), CancellationToken.None));
    }

    [Fact]
    public async Task Microsoft_cannot_be_unlinked()
    {
        var alice = await _service.SignInAsync(Microsoft(), CancellationToken.None);

        await Assert.ThrowsAsync<AccountRuleException>(() =>
            _service.UnlinkAsync(alice.UserId, IdentityProvider.Microsoft, CancellationToken.None));
    }

    [Fact]
    public async Task Unlinking_a_provider_the_user_never_linked_is_not_found()
    {
        var alice = await _service.SignInAsync(Microsoft(), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UnlinkAsync(alice.UserId, IdentityProvider.GitHub, CancellationToken.None));
    }

    [Fact]
    public async Task One_user_cannot_unlink_another_users_identity()
    {
        var alice = await _service.SignInAsync(Microsoft("ms-1", "Alice"), CancellationToken.None);
        var bob = await _service.SignInAsync(Microsoft("ms-2", "Bob"), CancellationToken.None);
        await _service.LinkAsync(alice.UserId, GitHub(), CancellationToken.None);

        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _service.UnlinkAsync(bob.UserId, IdentityProvider.GitHub, CancellationToken.None));

        Assert.True(_identities.Rows.ContainsKey((IdentityProvider.GitHub, "gh-1")));
    }

    [Fact]
    public async Task GetAccount_for_an_unknown_user_is_unauthorized()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetAccountAsync("ghost", CancellationToken.None));
    }
}
