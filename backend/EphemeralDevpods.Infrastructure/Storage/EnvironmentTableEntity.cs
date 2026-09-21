using Azure;
using Azure.Data.Tables;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Storage;

/// <summary>Table Storage row shape for the `environments` table. PartitionKey=owner, RowKey=environmentId.</summary>
public sealed class EnvironmentTableEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "";
    public string RowKey { get; set; } = "";
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string RepoUrl { get; set; } = "";
    public string Status { get; set; } = "";
    public int TtlMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastActivityAt { get; set; }
    public string? PublicUrl { get; set; }
    public string? AccessToken { get; set; }
    public string? TunnelName { get; set; }
    public bool TunnelReady { get; set; }

    public static EnvironmentTableEntity FromDomain(WorkspaceEnvironment env) => new()
    {
        PartitionKey = env.Owner,
        RowKey = env.EnvironmentId,
        RepoUrl = env.RepoUrl,
        Status = env.Status.ToString(),
        TtlMinutes = env.TtlMinutes,
        CreatedAt = env.CreatedAt,
        LastActivityAt = env.LastActivityAt,
        PublicUrl = env.PublicUrl,
        AccessToken = env.AccessToken,
        TunnelName = env.TunnelName,
        TunnelReady = env.TunnelReady,
    };

    public WorkspaceEnvironment ToDomain() => new()
    {
        Owner = PartitionKey,
        EnvironmentId = RowKey,
        RepoUrl = RepoUrl,
        Status = Enum.Parse<EnvironmentStatus>(Status),
        TtlMinutes = TtlMinutes,
        CreatedAt = CreatedAt,
        LastActivityAt = LastActivityAt,
        PublicUrl = PublicUrl,
        AccessToken = AccessToken,
        TunnelName = TunnelName,
        TunnelReady = TunnelReady,
    };
}
