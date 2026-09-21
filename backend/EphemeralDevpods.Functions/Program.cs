using Azure.Data.Tables;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Storage.Queues;
using EphemeralDevpods.Core.Auth;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Parsing;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Core.Repositories;
using EphemeralDevpods.Functions.Compute;
using EphemeralDevpods.Functions.Functions.Auth;
using EphemeralDevpods.Functions.Functions.Environments;
using EphemeralDevpods.Functions.Http;
using EphemeralDevpods.Infrastructure.Auth;
using EphemeralDevpods.Infrastructure.Git;
using EphemeralDevpods.Infrastructure.Provisioning;
using EphemeralDevpods.Infrastructure.Storage;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Azure.Functions.Worker.OpenTelemetry;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();
// Order matters: exception handling wraps auth so UnauthorizedException becomes a 401 response.
builder.UseMiddleware<ExceptionHandlingMiddleware>();
builder.UseMiddleware<AuthMiddleware>();

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

builder.Services.AddSingleton<IProvisioningQueue>(_ =>
{
    var client = new QueueClient(
        storageConnectionString, StorageProvisioningQueue.QueueName,
        new QueueClientOptions { MessageEncoding = QueueMessageEncoding.Base64 }); // what the Functions queue trigger expects
    client.CreateIfNotExists();
    return new StorageProvisioningQueue(client);
});

builder.Services.AddSingleton<IUserRepository>(_ =>
{
    var client = new TableServiceClient(storageConnectionString).GetTableClient("users");
    client.CreateIfNotExists();
    return new TableUserRepository(client);
});

builder.Services.AddSingleton<IIdentityRepository>(_ =>
{
    var client = new TableServiceClient(storageConnectionString).GetTableClient("identities");
    client.CreateIfNotExists();
    return new TableIdentityRepository(client);
});

// Auth: in-app OAuth (Microsoft + GitHub) with our own session cookie. Fail fast on missing config —
// every endpoint requires a session, so a half-configured app is useless anyway.
var authOptions = builder.Configuration.GetSection("Auth").Get<AuthOptions>() ?? new AuthOptions();
var microsoftOptions = builder.Configuration.GetSection("Auth:Microsoft").Get<MicrosoftOAuthOptions>() ?? new MicrosoftOAuthOptions();
var gitHubOptions = builder.Configuration.GetSection("Auth:GitHub").Get<GitHubOAuthOptions>() ?? new GitHubOAuthOptions();

var missingAuthSettings = new List<string>();
if (authOptions.SessionSigningKey.Length < AuthOptions.MinSigningKeyLength)
{
    missingAuthSettings.Add($"Auth:SessionSigningKey (at least {AuthOptions.MinSigningKeyLength} characters)");
}

if (string.IsNullOrWhiteSpace(microsoftOptions.ClientId)) missingAuthSettings.Add("Auth:Microsoft:ClientId");
if (string.IsNullOrWhiteSpace(microsoftOptions.ClientSecret)) missingAuthSettings.Add("Auth:Microsoft:ClientSecret");
if (string.IsNullOrWhiteSpace(gitHubOptions.ClientId)) missingAuthSettings.Add("Auth:GitHub:ClientId");
if (string.IsNullOrWhiteSpace(gitHubOptions.ClientSecret)) missingAuthSettings.Add("Auth:GitHub:ClientSecret");

if (missingAuthSettings.Count > 0)
{
    throw new InvalidOperationException(
        "Missing auth configuration: " + string.Join(", ", missingAuthSettings) +
        ". See the 'Auth setup' section of README.md (in local.settings.json use '__' instead of ':', e.g. Auth__Microsoft__ClientId).");
}

builder.Services.AddSingleton(authOptions);
builder.Services.AddSingleton(microsoftOptions);
builder.Services.AddSingleton(gitHubOptions);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<SessionTokenService>();
builder.Services.AddSingleton<AccountService>();
builder.Services.AddSingleton<OAuthFlow>();
builder.Services.AddHttpClient<MicrosoftOAuthProvider>();
builder.Services.AddHttpClient<GitHubOAuthProvider>();
builder.Services.AddTransient<IOAuthProvider>(sp => sp.GetRequiredService<MicrosoftOAuthProvider>());
builder.Services.AddTransient<IOAuthProvider>(sp => sp.GetRequiredService<GitHubOAuthProvider>());

// Compute backend (Docker locally, ACI in the cloud) is chosen by Compute:Provider; see ComputeRegistration.
builder.Services.AddCompute(builder.Configuration);

builder.Services.AddSingleton<EnvironmentStatusSync>();
builder.Services.AddSingleton<ProvisioningGate>();
builder.Services.AddSingleton<EnvironmentLifecycle>();
builder.Services.AddSingleton<EnvironmentProvisioning>();
builder.Services.AddSingleton<RepoInspector>();
builder.Services.AddSingleton<HostPortSelector>();

builder.Build().Run();
