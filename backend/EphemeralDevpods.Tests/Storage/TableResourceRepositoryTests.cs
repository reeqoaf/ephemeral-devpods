using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Storage;

namespace EphemeralDevpods.Tests.Storage;

public class TableResourceRepositoryTests(AzuriteFixture fixture) : IClassFixture<AzuriteFixture>
{
    private readonly TableResourceRepository _repo = new(fixture.ResourcesTable);

    private static DeployedResource MakeResource(string environmentId, string resourceId) => new()
    {
        EnvironmentId = environmentId,
        ResourceId = resourceId,
        Type = ResourceType.Container,
        ProviderResourceId = "/subscriptions/.../containerGroups/example",
        Image = "node:20",
        Port = 3000,
        Status = "Running",
    };

    [Fact]
    public async Task Upsert_then_list_round_trips()
    {
        var environmentId = Guid.NewGuid().ToString();
        var resource = MakeResource(environmentId, "container-1");

        await _repo.UpsertAsync(resource, CancellationToken.None);
        var results = await _repo.ListByEnvironmentAsync(environmentId, CancellationToken.None);

        var loaded = Assert.Single(results);
        Assert.Equal(resource.ProviderResourceId, loaded.ProviderResourceId);
        Assert.Equal(resource.Type, loaded.Type);
        Assert.Equal(resource.Port, loaded.Port);
    }

    [Fact]
    public async Task List_only_returns_rows_for_that_environment()
    {
        var environmentId = Guid.NewGuid().ToString();
        var otherEnvironmentId = Guid.NewGuid().ToString();

        await _repo.UpsertAsync(MakeResource(environmentId, "container-1"), CancellationToken.None);
        await _repo.UpsertAsync(MakeResource(environmentId, "container-2"), CancellationToken.None);
        await _repo.UpsertAsync(MakeResource(otherEnvironmentId, "container-1"), CancellationToken.None);

        var results = await _repo.ListByEnvironmentAsync(environmentId, CancellationToken.None);

        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Equal(environmentId, r.EnvironmentId));
    }

    [Fact]
    public async Task Delete_removes_the_row()
    {
        var environmentId = Guid.NewGuid().ToString();
        await _repo.UpsertAsync(MakeResource(environmentId, "container-1"), CancellationToken.None);

        await _repo.DeleteAsync(environmentId, "container-1", CancellationToken.None);
        var results = await _repo.ListByEnvironmentAsync(environmentId, CancellationToken.None);

        Assert.Empty(results);
    }
}
