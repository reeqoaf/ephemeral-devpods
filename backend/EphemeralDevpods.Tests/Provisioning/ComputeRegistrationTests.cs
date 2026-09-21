using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Functions.Compute;
using EphemeralDevpods.Infrastructure.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning.Aci;
using EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EphemeralDevpods.Tests.Provisioning;

public class ComputeRegistrationTests
{
    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static Dictionary<string, string?> ValidAci() => new()
    {
        ["Compute:Provider"] = "Aci",
        ["Compute:Aci:TenantId"] = "00000000-0000-0000-0000-000000000001",
        ["Compute:Aci:ClientId"] = "00000000-0000-0000-0000-000000000002",
        ["Compute:Aci:ClientSecret"] = "secret",
        ["Compute:Aci:SubscriptionId"] = "00000000-0000-0000-0000-000000000003",
        ["Compute:Aci:ResourceGroup"] = "rg-envs",
        ["Compute:Aci:Location"] = "westeurope",
        ["Compute:Aci:RegistryName"] = "epdregistry",
        ["Compute:Aci:RegistryResourceGroup"] = "rg-platform",
        ["AzureWebJobsStorage"] = "UseDevelopmentStorage=true",
    };

    [Fact]
    public void Defaults_to_the_local_docker_provisioner_with_no_configuration()
    {
        var services = new ServiceCollection();

        var options = services.AddCompute(Config([]));

        Assert.Equal(ComputeProvider.Docker, options.Provider);
        var descriptor = Assert.Single(services, d => d.ServiceType == typeof(IComputeProvisioner));
        Assert.Equal(typeof(LocalDockerProvisioner), descriptor.ImplementationType);
    }

    [Fact]
    public void Aci_resolves_the_aci_provisioner()
    {
        var services = new ServiceCollection();
        services.AddCompute(Config(ValidAci()));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AciProvisioner>(provider.GetRequiredService<IComputeProvisioner>());
        Assert.False(provider.GetRequiredService<IComputeProvisioner>().PublishesHostPorts);
    }

    [Fact]
    public void Aci_does_not_register_the_docker_client()
    {
        var services = new ServiceCollection();
        services.AddCompute(Config(ValidAci()));

        Assert.DoesNotContain(services, d => d.ServiceType == typeof(Docker.DotNet.IDockerClient));
    }

    [Fact]
    public void Aci_with_missing_settings_fails_fast_naming_every_one()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new ServiceCollection().AddCompute(Config(new() { ["Compute:Provider"] = "Aci", ["AzureWebJobsStorage"] = "x" })));

        Assert.Contains("Compute:Aci:TenantId", error.Message);
        Assert.Contains("Compute:Aci:RegistryResourceGroup", error.Message);
    }

    [Fact]
    public void Aci_refuses_to_start_without_real_storage_instead_of_falling_back_to_the_emulator()
    {
        var values = ValidAci();
        values.Remove("AzureWebJobsStorage");

        var error = Assert.Throws<InvalidOperationException>(() => new ServiceCollection().AddCompute(Config(values)));

        Assert.Contains("AzureWebJobsStorage", error.Message);
    }
}
