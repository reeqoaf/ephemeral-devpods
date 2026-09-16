using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Provisioning;

public sealed class ProvisionResult
{
    public required string PublicUrl { get; init; }
    public required string AccessToken { get; init; }
    public required IReadOnlyList<DeployedResource> Resources { get; init; }
}
