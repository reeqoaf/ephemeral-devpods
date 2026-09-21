using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage row shape for the `users` table. PartitionKey is a constant, RowKey=userId.</summary>
public sealed class UserTableEntity : ITableEntity
{
    public const string Partition = "user";

    public string PartitionKey { get; set; } = Partition;
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public bool IsAdmin { get; set; }

    public static UserTableEntity FromDomain(User user) => new()
    {
        RowKey = user.UserId,
        DisplayName = user.DisplayName,
        Email = user.Email,
        CreatedAt = user.CreatedAt,
        IsAdmin = user.IsAdmin,
    };

    public User ToDomain() => new()
    {
        UserId = RowKey,
        DisplayName = DisplayName,
        Email = Email,
        CreatedAt = CreatedAt,
        IsAdmin = IsAdmin,
    };
}
