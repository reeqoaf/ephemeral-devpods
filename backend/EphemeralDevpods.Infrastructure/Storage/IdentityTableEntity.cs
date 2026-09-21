using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage row shape for the `identities` table. PartitionKey=provider, RowKey=provider subject.</summary>
public sealed class IdentityTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTimeOffset LinkedAt { get; set; }

    public static IdentityTableEntity FromDomain(LinkedIdentity identity) => new()
    {
        PartitionKey = identity.Provider.ToString(),
        RowKey = identity.Subject,
        UserId = identity.UserId,
        DisplayName = identity.DisplayName,
        LinkedAt = identity.LinkedAt,
    };

    public LinkedIdentity ToDomain() => new()
    {
        Provider = Enum.Parse<IdentityProvider>(PartitionKey),
        Subject = RowKey,
        UserId = UserId,
        DisplayName = DisplayName,
        LinkedAt = LinkedAt,
    };
}
