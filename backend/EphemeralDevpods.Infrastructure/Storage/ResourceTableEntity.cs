using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage row shape for the `resources` table. PartitionKey=environmentId, RowKey=resourceId.</summary>
public sealed class ResourceTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string Type { get; set; } = "";
    public string ProviderResourceId { get; set; } = "";
    public string? Image { get; set; }
    public int? Port { get; set; }
    public string Status { get; set; } = "";

    public static ResourceTableEntity FromDomain(DeployedResource resource) => new()
    {
        PartitionKey = resource.EnvironmentId,
        RowKey = resource.ResourceId,
        Type = resource.Type.ToString(),
        ProviderResourceId = resource.ProviderResourceId,
        Image = resource.Image,
        Port = resource.Port,
        Status = resource.Status,
    };

    public DeployedResource ToDomain() => new()
    {
        EnvironmentId = PartitionKey,
        ResourceId = RowKey,
        Type = Enum.Parse<ResourceType>(Type),
        ProviderResourceId = ProviderResourceId,
        Image = Image,
        Port = Port,
        Status = Status,
    };
}
