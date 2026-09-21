namespace EphemeralDevpods.Infrastructure.Provisioning;

public enum ComputeProvider
{
    /// <summary>Local development: environments are Docker containers on this machine.</summary>
    Docker,

    /// <summary>Production: one Azure Container Instances container group per environment.</summary>
    Aci,
}

/// <summary>Which compute backend runs environments (config section "Compute"). Defaults to local Docker.</summary>
public sealed class ComputeOptions
{
    public const string SectionName = "Compute";

    public ComputeProvider Provider { get; set; } = ComputeProvider.Docker;

    public AciOptions Aci { get; set; } = new();

    /// <summary>Config keys the selected provider needs but doesn't have; empty when the configuration is usable.</summary>
    public IReadOnlyList<string> MissingSettings()
    {
        if (Provider != ComputeProvider.Aci)
        {
            return [];
        }

        var missing = new List<string>();
        Require(missing, Aci.TenantId, "TenantId");
        Require(missing, Aci.ClientId, "ClientId");
        Require(missing, Aci.ClientSecret, "ClientSecret");
        Require(missing, Aci.SubscriptionId, "SubscriptionId");
        Require(missing, Aci.ResourceGroup, "ResourceGroup");
        Require(missing, Aci.Location, "Location");
        Require(missing, Aci.RegistryName, "RegistryName");
        Require(missing, Aci.RegistryResourceGroup, "RegistryResourceGroup");

        // Pull credentials are a pair: one without the other can never authenticate.
        var hasPullId = !string.IsNullOrWhiteSpace(Aci.PullClientId);
        var hasPullSecret = !string.IsNullOrWhiteSpace(Aci.PullClientSecret);
        if (hasPullId != hasPullSecret)
        {
            missing.Add(hasPullId ? $"{SectionName}:Aci:PullClientSecret" : $"{SectionName}:Aci:PullClientId");
        }

        return missing;
    }

    private static void Require(List<string> missing, string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            missing.Add($"{SectionName}:Aci:{name}");
        }
    }
}

/// <summary>
/// Everything AciProvisioner needs. Authentication is a service principal only (no managed identity):
/// it needs Contributor on <see cref="ResourceGroup"/> and permission to run builds on the registry.
/// </summary>
public sealed class AciOptions
{
    public string TenantId { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string SubscriptionId { get; set; } = "";

    /// <summary>Runtime resource group holding every environment's container group (not the platform group).</summary>
    public string ResourceGroup { get; set; } = "";

    public string Location { get; set; } = "";

    /// <summary>Azure Container Registry (name only, no domain) that builds are pushed to and pulled from.</summary>
    public string RegistryName { get; set; } = "";

    /// <summary>Resource group of the registry: platform infrastructure, so normally not <see cref="ResourceGroup"/>.</summary>
    public string RegistryResourceGroup { get; set; } = "";

    /// <summary>
    /// Optional AcrPull-only service principal handed to container groups as image-pull credentials, so
    /// the main principal's secret never leaves this app. Only needed for images built into the registry.
    /// </summary>
    public string? PullClientId { get; set; }

    public string? PullClientSecret { get; set; }

    public string RegistryLoginServer => $"{RegistryName}.azurecr.io";
}
