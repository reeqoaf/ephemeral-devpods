using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using Docker.DotNet;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Parsing;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Functions.Environments;
using EphemeralDevpods.Infrastructure.Git;
using EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;
using EphemeralDevpods.Infrastructure.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING")))
{
    builder.Services.AddOpenTelemetry()
        .UseFunctionsWorkerDefaults()
        .UseAzureMonitorExporter();
}

var storageConnectionString = builder.Configuration["AzureWebJobsStorage"] ?? "UseDevelopmentStorage=true";

builder.Services.AddHttpClient<IDevcontainerFileFetcher, GitHubDevcontainerFetcher>();
builder.Services.AddHttpClient<IBuildContextFetcher, GitHubBuildContextFetcher>();

builder.Services.AddSingleton<IDevcontainerParser, DevcontainerParser>();

builder.Services.AddSingleton<IEnvironmentRepository>(_ =>
{
    var client = new TableServiceClient(storageConnectionString).GetTableClient("environments");
    client.CreateIfNotExists();
    return new TableEnvironmentRepository(client);
});

builder.Services.AddSingleton<IResourceRepository>(_ =>
{
    var client = new TableServiceClient(storageConnectionString).GetTableClient("resources");
    client.CreateIfNotExists();
    return new TableResourceRepository(client);
});

// LocalDockerProvisioner only — this is the local-dev compute backend (docs/spec.md §13).
// AciProvisioner is stubbed until the cloud-deployment phase.
builder.Services.AddSingleton<IDockerClient>(_ =>
{
    var endpoint = OperatingSystem.IsWindows()
        ? new Uri("npipe://./pipe/docker_engine")
        : new Uri("unix:///var/run/docker.sock");
    return new DockerClientConfiguration(endpoint).CreateClient();
});
builder.Services.AddSingleton<IComputeProvisioner, LocalDockerProvisioner>();

builder.Services.AddSingleton<EnvironmentStatusSync>();

builder.Build().Run();
