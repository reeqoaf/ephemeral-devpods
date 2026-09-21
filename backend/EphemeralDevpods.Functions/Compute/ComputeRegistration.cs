using Azure.Identity;
using Azure.ResourceManager;
using Docker.DotNet;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning.Aci;
using EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EphemeralDevpods.Functions.Compute;

public static class ComputeRegistration
{
    /// <summary>
    /// Registers the compute backend chosen by <c>Compute:Provider</c> (docs/spec.md §13). Fails fast on missing
    /// settings, like the auth configuration — a half-configured cloud backend would only fail on the first create.
    /// </summary>
    public static ComputeOptions AddCompute(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(ComputeOptions.SectionName).Get<ComputeOptions>() ?? new ComputeOptions();

        var missing = options.MissingSettings().ToList();
        if (options.Provider == ComputeProvider.Aci && string.IsNullOrWhiteSpace(configuration["AzureWebJobsStorage"]))
        {
            // Without this the storage fallback would silently point production at the local emulator.
            missing.Add("AzureWebJobsStorage");
        }

        if (missing.Count > 0)
        {
            throw new InvalidOperationException(
                "Missing compute configuration: " + string.Join(", ", missing) +
                ". See the 'Compute setup' section of README.md (in local.settings.json use '__' instead of ':').");
        }

        services.AddSingleton(options);

        switch (options.Provider)
        {
            case ComputeProvider.Docker:
                services.AddSingleton<IDockerClient>(_ =>
                {
                    var endpoint = OperatingSystem.IsWindows()
                        ? new Uri("npipe://./pipe/docker_engine")
                        : new Uri("unix:///var/run/docker.sock");
                    return new DockerClientConfiguration(endpoint).CreateClient();
                });
                services.AddSingleton<IComputeProvisioner, LocalDockerProvisioner>();
                break;

            case ComputeProvider.Aci:
                services.AddSingleton(options.Aci);
                services.AddSingleton(_ => new ArmClient(
                    new ClientSecretCredential(options.Aci.TenantId, options.Aci.ClientId, options.Aci.ClientSecret),
                    options.Aci.SubscriptionId));
                services.AddSingleton<IComputeProvisioner, AciProvisioner>();
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(options), options.Provider, "Unknown compute provider.");
        }

        return options;
    }
}
