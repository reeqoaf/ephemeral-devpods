using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Tests.Environments;

public class PortMappingTests
{
    [Fact]
    public void Identity_publishes_each_port_at_its_own_number_in_order()
    {
        Assert.Equal([new PortMapping(3000, 3000), new PortMapping(5173, 5173)], PortMapping.Identity([3000, 5173]));
    }

    [Fact]
    public void Identity_ignores_repeated_ports()
    {
        Assert.Single(PortMapping.Identity([3000, 3000]));
    }

    [Fact]
    public void Formats_as_container_colon_host_pairs()
    {
        Assert.Equal("3000:8081,5173:8082", PortMapping.Format([new(3000, 8081), new(5173, 8082)]));
    }

    [Fact]
    public void Parses_what_it_formats()
    {
        var mappings = new List<PortMapping> { new(3000, 8081), new(5173, 8082) };

        Assert.Equal(mappings, PortMapping.ParseList(PortMapping.Format(mappings)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Nothing_stored_means_no_mappings(string? stored)
    {
        Assert.Empty(PortMapping.ParseList(stored));
    }
}
