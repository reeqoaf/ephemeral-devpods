using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Tests.Environments;

public class EnvironmentOptionsTests
{
    private static EnvironmentOptions Resolve(
        string? name = "my-pod", int? ttl = null, int? cpu = null, int? memory = null, string? provider = null) =>
        EnvironmentOptions.Resolve(name, ttl, cpu, memory, provider, fallbackName: "fallback");

    [Fact]
    public void Missing_options_fall_back_to_the_defaults()
    {
        var options = Resolve();

        Assert.Equal(60, options.TtlMinutes);
        Assert.Equal(2, options.CpuCores);
        Assert.Equal(4096, options.MemoryMb);
        Assert.Equal(TunnelProvider.GitHub, options.TunnelProvider);
    }

    [Fact]
    public void Offered_values_are_accepted()
    {
        var options = Resolve(ttl: 60, cpu: 2, memory: 4096, provider: "github");

        Assert.Equal(60, options.TtlMinutes);
        Assert.Equal(TunnelProvider.GitHub, options.TunnelProvider);
    }

    [Theory]
    [InlineData(30, null, null)]
    [InlineData(null, 8, null)]
    [InlineData(null, null, 1024)]
    public void Values_outside_the_offered_sets_are_rejected(int? ttl, int? cpu, int? memory)
    {
        Assert.Throws<InvalidEnvironmentOptionException>(() => Resolve(ttl: ttl, cpu: cpu, memory: memory));
    }

    [Fact]
    public void Microsoft_is_recognised_but_not_yet_supported()
    {
        var ex = Assert.Throws<InvalidEnvironmentOptionException>(() => Resolve(provider: "Microsoft"));

        Assert.Contains("isn't supported yet", ex.Message);
    }

    [Fact]
    public void An_unknown_provider_is_rejected()
    {
        Assert.Throws<InvalidEnvironmentOptionException>(() => Resolve(provider: "gitlab"));
    }

    [Fact]
    public void The_name_is_trimmed()
    {
        Assert.Equal("my pod", Resolve(name: "  my pod  ").Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_blank_name_falls_back_to_the_suggested_one(string? name)
    {
        Assert.Equal("fallback", Resolve(name: name).Name);
    }

    [Fact]
    public void A_name_over_the_limit_is_rejected()
    {
        Assert.Throws<InvalidEnvironmentOptionException>(
            () => Resolve(name: new string('a', EnvironmentOptions.MaxNameLength + 1)));
        Assert.Equal(EnvironmentOptions.MaxNameLength, Resolve(name: new string('a', EnvironmentOptions.MaxNameLength)).Name.Length);
    }
}
