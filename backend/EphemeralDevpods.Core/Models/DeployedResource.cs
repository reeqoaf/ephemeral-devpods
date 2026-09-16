namespace EphemeralDevpods.Core.Models;

public enum ResourceType
{
    ContainerGroup,
    Container,
    NetworkInterface,
    PublicIp,
}

/// <summary>Mirrors a row in the `resources` Table Storage table.</summary>
public sealed class DeployedResource
{
    public required string EnvironmentId { get; init; }
    public required string ResourceId { get; init; }
    public required ResourceType Type { get; init; }
    public required string ProviderResourceId { get; init; }
    public string? Image { get; init; }
    public int? Port { get; init; }
    public required string Status { get; set; }
}
