using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Http;
using EphemeralDevpods.Infrastructure.Provisioning;
using EphemeralDevpods.Infrastructure.Storage;

namespace EphemeralDevpods.Tests.Auth;

public class ProvisioningGateTests
{
    private sealed class FakeUsers : IUserRepository
    {
        public Dictionary<string, User> Rows { get; } = [];
        public int Lookups { get; private set; }

        public Task<User?> GetAsync(string userId, CancellationToken ct)
        {
            Lookups++;
            return Task.FromResult(Rows.GetValueOrDefault(userId));
        }

        public Task AddAsync(User user, CancellationToken ct)
        {
            Rows[user.UserId] = user;
            return Task.CompletedTask;
        }
    }

    private static User MakeUser(bool isAdmin) => new()
    {
        UserId = "u1",
        DisplayName = "User One",
        IsAdmin = isAdmin,
        CreatedAt = DateTimeOffset.UtcNow,
    };

    private static ProvisioningGate Gate(FakeUsers users, ComputeProvider provider) =>
        new(users, new ComputeOptions { Provider = provider });

    [Fact]
    public async Task An_admin_may_provision_on_aci()
    {
        var users = new FakeUsers();
        users.Rows["u1"] = MakeUser(isAdmin: true);

        await Gate(users, ComputeProvider.Aci).EnsureAllowedAsync("u1", CancellationToken.None);
    }

    [Fact]
    public async Task A_non_admin_is_forbidden_on_aci()
    {
        var users = new FakeUsers();
        users.Rows["u1"] = MakeUser(isAdmin: false);

        await Assert.ThrowsAsync<ForbiddenException>(
            () => Gate(users, ComputeProvider.Aci).EnsureAllowedAsync("u1", CancellationToken.None));
    }

    [Fact]
    public async Task A_missing_account_is_unauthorized_on_aci()
    {
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => Gate(new FakeUsers(), ComputeProvider.Aci).EnsureAllowedAsync("ghost", CancellationToken.None));
    }

    [Fact]
    public async Task Local_docker_is_open_to_everyone_without_a_lookup()
    {
        var users = new FakeUsers();
        users.Rows["u1"] = MakeUser(isAdmin: false);

        await Gate(users, ComputeProvider.Docker).EnsureAllowedAsync("u1", CancellationToken.None);

        Assert.Equal(0, users.Lookups);
    }

    [Theory]
    [InlineData(ComputeProvider.Aci, true, true)]
    [InlineData(ComputeProvider.Aci, false, false)]
    [InlineData(ComputeProvider.Docker, true, true)]
    [InlineData(ComputeProvider.Docker, false, true)]
    public void IsAllowed_reflects_the_provider_and_the_admin_flag(ComputeProvider provider, bool isAdmin, bool expected)
    {
        Assert.Equal(expected, Gate(new FakeUsers(), provider).IsAllowed(MakeUser(isAdmin)));
    }

    [Fact]
    public void The_admin_flag_round_trips_through_the_table_entity()
    {
        var entity = UserTableEntity.FromDomain(MakeUser(isAdmin: true));

        Assert.True(entity.IsAdmin);
        Assert.True(entity.ToDomain().IsAdmin);
    }

    [Fact]
    public void Rows_written_before_the_flag_existed_are_not_admins()
    {
        // A missing column deserializes to the property default.
        Assert.False(new UserTableEntity { RowKey = "u1", DisplayName = "Old row" }.ToDomain().IsAdmin);
    }
}
