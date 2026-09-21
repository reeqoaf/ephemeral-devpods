using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Storage;

namespace EphemeralDevpods.Tests.Storage;

public class TableEnvironmentRepositoryTests(AzuriteFixture fixture) : IClassFixture<AzuriteFixture>
{
    private readonly TableEnvironmentRepository _repo = new(fixture.EnvironmentsTable);

    private static WorkspaceEnvironment MakeEnv(
        string owner, string environmentId, EnvironmentStatus status = EnvironmentStatus.Running,
        int ttlMinutes = 60, DateTimeOffset? createdAt = null) => new()
    {
        Owner = owner,
        EnvironmentId = environmentId,
        RepoUrl = "https://github.com/owner/repo",
        Status = status,
        TtlMinutes = ttlMinutes,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
    };

    [Fact]
    public async Task Upsert_then_get_round_trips()
    {
        var env = MakeEnv("alice", Guid.NewGuid().ToString());

        await _repo.UpsertAsync(env, CancellationToken.None);
        var loaded = await _repo.GetAsync("alice", env.EnvironmentId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal(env.RepoUrl, loaded.RepoUrl);
        Assert.Equal(env.Status, loaded.Status);
        Assert.Equal(env.TtlMinutes, loaded.TtlMinutes);
    }

    [Fact]
    public async Task Tunnel_fields_round_trip()
    {
        var env = MakeEnv("alice", Guid.NewGuid().ToString());
        env.TunnelName = "epd-12345678";
        env.TunnelReady = true;

        await _repo.UpsertAsync(env, CancellationToken.None);
        var loaded = await _repo.GetAsync("alice", env.EnvironmentId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Equal("epd-12345678", loaded.TunnelName);
        Assert.True(loaded.TunnelReady);
    }

    [Fact]
    public async Task Rows_without_tunnel_fields_load_as_not_ready()
    {
        var env = MakeEnv("alice", Guid.NewGuid().ToString());

        await _repo.UpsertAsync(env, CancellationToken.None);
        var loaded = await _repo.GetAsync("alice", env.EnvironmentId, CancellationToken.None);

        Assert.NotNull(loaded);
        Assert.Null(loaded.TunnelName);
        Assert.False(loaded.TunnelReady);
    }

    [Fact]
    public async Task Get_returns_null_when_missing()
    {
        var loaded = await _repo.GetAsync("nobody", "missing-id", CancellationToken.None);

        Assert.Null(loaded);
    }

    [Fact]
    public async Task ListByOwner_returns_only_that_owners_rows()
    {
        var owner = Guid.NewGuid().ToString();
        var other = Guid.NewGuid().ToString();

        await _repo.UpsertAsync(MakeEnv(owner, Guid.NewGuid().ToString()), CancellationToken.None);
        await _repo.UpsertAsync(MakeEnv(owner, Guid.NewGuid().ToString()), CancellationToken.None);
        await _repo.UpsertAsync(MakeEnv(other, Guid.NewGuid().ToString()), CancellationToken.None);

        var results = await _repo.ListByOwnerAsync(owner, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(owner, r.Owner));
    }

    [Fact]
    public async Task Get_returns_null_when_the_environment_belongs_to_another_owner()
    {
        var alice = Guid.NewGuid().ToString();
        var bob = Guid.NewGuid().ToString();
        var env = MakeEnv(alice, Guid.NewGuid().ToString());
        await _repo.UpsertAsync(env, CancellationToken.None);

        // The id is valid and exists, but only under alice's partition — this is what the endpoints' 404 relies on.
        Assert.Null(await _repo.GetAsync(bob, env.EnvironmentId, CancellationToken.None));
        Assert.Empty(await _repo.ListByOwnerAsync(bob, CancellationToken.None));
        Assert.NotNull(await _repo.GetAsync(alice, env.EnvironmentId, CancellationToken.None));
    }

    [Fact]
    public async Task ListExpiringBefore_includes_only_past_ttl_running_or_provisioning_rows()
    {
        var owner = Guid.NewGuid().ToString();
        var now = DateTimeOffset.UtcNow;

        var expired = MakeEnv(owner, Guid.NewGuid().ToString(), EnvironmentStatus.Running, ttlMinutes: 10,
            createdAt: now.AddMinutes(-30));
        var stillAlive = MakeEnv(owner, Guid.NewGuid().ToString(), EnvironmentStatus.Running, ttlMinutes: 120,
            createdAt: now.AddMinutes(-10));
        var alreadyExpiredStatus = MakeEnv(owner, Guid.NewGuid().ToString(), EnvironmentStatus.Expired, ttlMinutes: 10,
            createdAt: now.AddMinutes(-30));
        var provisioningPastTtl = MakeEnv(owner, Guid.NewGuid().ToString(), EnvironmentStatus.Provisioning, ttlMinutes: 5,
            createdAt: now.AddMinutes(-30));

        await _repo.UpsertAsync(expired, CancellationToken.None);
        await _repo.UpsertAsync(stillAlive, CancellationToken.None);
        await _repo.UpsertAsync(alreadyExpiredStatus, CancellationToken.None);
        await _repo.UpsertAsync(provisioningPastTtl, CancellationToken.None);

        var results = await _repo.ListExpiringBeforeAsync(now, CancellationToken.None);
        var resultIds = results.Select(r => r.EnvironmentId).ToHashSet();

        Assert.Contains(expired.EnvironmentId, resultIds);
        Assert.Contains(provisioningPastTtl.EnvironmentId, resultIds);
        Assert.DoesNotContain(stillAlive.EnvironmentId, resultIds);
        Assert.DoesNotContain(alreadyExpiredStatus.EnvironmentId, resultIds);
    }
}
