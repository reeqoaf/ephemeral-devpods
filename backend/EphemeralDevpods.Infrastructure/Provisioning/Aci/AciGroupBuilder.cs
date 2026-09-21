using Azure.ResourceManager.ContainerInstance;
using Azure.ResourceManager.ContainerInstance.Models;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Provisioning.Aci;

/// <summary>
/// Turns an environment into the ACI container group that runs it. Pure (no Azure calls), so the shape of what we
/// deploy is unit-testable.
/// </summary>
public static class AciGroupBuilder
{
    public const string ContainerName = "workspace";

    private const int MaxTagValueLength = 256;

    public static string GroupName(string environmentId) => $"epd-{environmentId}";

    /// <summary>
    /// One container, the injected entrypoint as its command, and deliberately no IP address: nothing is reachable
    /// from the internet, users get in through the VS Code tunnel (outbound only). Restart policy Never so a crashed
    /// environment shows as failed instead of looping.
    /// </summary>
    public static ContainerGroupData Build(
        AciOptions options, WorkspaceEnvironment environment, EnvironmentSpec spec, string image, string entrypointScript)
    {
        // Environments created before limits existed have none; fall back to what is offered today.
        var cpuCores = environment.CpuCores ?? EnvironmentOptions.AllowedCpuCores[0];
        var memoryGb = (environment.MemoryMb ?? EnvironmentOptions.AllowedMemoryMb[0]) / 1024.0;

        var container = new ContainerInstanceContainer(
            ContainerName, image, new ContainerResourceRequirements(new ContainerResourceRequestsContent(memoryGb, cpuCores)));
        container.Command.Add("sh");
        container.Command.Add("-c");
        container.Command.Add(entrypointScript);
        foreach (var (name, value) in spec.ContainerEnv)
        {
            container.EnvironmentVariables.Add(new ContainerEnvironmentVariable(name) { Value = value });
        }

        var group = new ContainerGroupData(
            new Azure.Core.AzureLocation(options.Location), [container], ContainerInstanceOperatingSystemType.Linux)
        {
            RestartPolicy = ContainerGroupRestartPolicy.Never,
        };

        // Spec section 7: every resource is tagged so cost and ownership can be traced without our own tables.
        group.Tags["environmentId"] = environment.EnvironmentId;
        group.Tags["owner"] = environment.Owner;
        group.Tags["repo"] = Truncate(environment.RepoUrl);
        group.Tags["ttl"] = environment.TtlMinutes.ToString();
        group.Tags["createdAt"] = environment.CreatedAt.ToString("O");

        if (IsFromOurRegistry(options, image) && HasPullCredentials(options))
        {
            group.ImageRegistryCredentials.Add(
                new ContainerGroupImageRegistryCredential(options.RegistryLoginServer)
                {
                    Username = options.PullClientId,
                    Password = options.PullClientSecret,
                });
        }

        return group;
    }

    public static bool IsFromOurRegistry(AciOptions options, string image) =>
        image.StartsWith(options.RegistryLoginServer + "/", StringComparison.OrdinalIgnoreCase);

    public static bool HasPullCredentials(AciOptions options) =>
        !string.IsNullOrWhiteSpace(options.PullClientId) && !string.IsNullOrWhiteSpace(options.PullClientSecret);

    private static string Truncate(string value) =>
        value.Length <= MaxTagValueLength ? value : value[..MaxTagValueLength];
}
