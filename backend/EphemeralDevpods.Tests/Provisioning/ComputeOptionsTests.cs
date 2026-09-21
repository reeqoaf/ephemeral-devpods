using EphemeralDevpods.Infrastructure.Provisioning;

namespace EphemeralDevpods.Tests.Provisioning;

public class ComputeOptionsTests
{
    private static ComputeOptions ValidAci() => new()
    {
        Provider = ComputeProvider.Aci,
        Aci = new AciOptions
        {
            TenantId = "tenant",
            ClientId = "client",
            ClientSecret = "secret",
            SubscriptionId = "sub",
            ResourceGroup = "rg-envs",
            Location = "westeurope",
            RegistryName = "epdregistry",
            RegistryResourceGroup = "rg-platform",
        },
    };

    [Fact]
    public void Defaults_to_local_docker_which_needs_no_settings()
    {
        var options = new ComputeOptions();

        Assert.Equal(ComputeProvider.Docker, options.Provider);
        Assert.Empty(options.MissingSettings());
    }

    [Fact]
    public void Docker_ignores_unset_aci_settings()
    {
        Assert.Empty(new ComputeOptions { Provider = ComputeProvider.Docker }.MissingSettings());
    }

    [Fact]
    public void A_complete_aci_configuration_is_valid()
    {
        Assert.Empty(ValidAci().MissingSettings());
    }

    [Fact]
    public void Aci_reports_every_missing_setting_at_once()
    {
        var missing = new ComputeOptions { Provider = ComputeProvider.Aci }.MissingSettings();

        Assert.Equal(
            [
                "Compute:Aci:TenantId", "Compute:Aci:ClientId", "Compute:Aci:ClientSecret", "Compute:Aci:SubscriptionId",
                "Compute:Aci:ResourceGroup", "Compute:Aci:Location", "Compute:Aci:RegistryName",
                "Compute:Aci:RegistryResourceGroup",
            ],
            missing);
    }

    [Theory]
    [InlineData("pull-id", null, "Compute:Aci:PullClientSecret")]
    [InlineData(null, "pull-secret", "Compute:Aci:PullClientId")]
    public void Pull_credentials_must_be_given_as_a_pair(string? id, string? secret, string expectedMissing)
    {
        var options = ValidAci();
        options.Aci.PullClientId = id;
        options.Aci.PullClientSecret = secret;

        Assert.Equal([expectedMissing], options.MissingSettings());
    }

    [Fact]
    public void Pull_credentials_are_optional()
    {
        var options = ValidAci();
        options.Aci.PullClientId = null;
        options.Aci.PullClientSecret = null;

        Assert.Empty(options.MissingSettings());
    }

    [Fact]
    public void Registry_login_server_is_derived_from_the_registry_name()
    {
        Assert.Equal("epdregistry.azurecr.io", ValidAci().Aci.RegistryLoginServer);
    }
}
