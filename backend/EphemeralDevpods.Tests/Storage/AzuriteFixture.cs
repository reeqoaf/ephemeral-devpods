using Azure.Data.Tables;

namespace EphemeralDevpods.Tests.Storage;

/// <summary>
/// Spins up isolated, uniquely-named tables against a locally running Azurite instance
/// (see docs/local-azurite.md — `docker compose -f docker-compose.dev.yml up -d`).
/// Requires Azurite to be running; these are integration tests, not pure unit tests.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    private const string ConnectionString = "UseDevelopmentStorage=true";

    public TableClient EnvironmentsTable { get; private set; } = null!;
    public TableClient ResourcesTable { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var serviceClient = new TableServiceClient(ConnectionString);
        var suffix = Guid.NewGuid().ToString("N")[..12];

        EnvironmentsTable = serviceClient.GetTableClient($"testenvironments{suffix}");
        ResourcesTable = serviceClient.GetTableClient($"testresources{suffix}");

        await EnvironmentsTable.CreateIfNotExistsAsync();
        await ResourcesTable.CreateIfNotExistsAsync();
    }

    public async Task DisposeAsync()
    {
        await EnvironmentsTable.DeleteAsync();
        await ResourcesTable.DeleteAsync();
    }
}
