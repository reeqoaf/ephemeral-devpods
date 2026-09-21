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
    public string? Name { get; set; }
    public string Status { get; set; } = "";
    public int TtlMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public int? CpuCores { get; set; }
    public int? MemoryMb { get; set; }
    public string? TunnelProvider { get; set; }
    public string? PortMappings { get; set; }
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
        Name = env.Name,
        Status = env.Status.ToString(),
        TtlMinutes = env.TtlMinutes,
        CreatedAt = env.CreatedAt,
        CpuCores = env.CpuCores,
        MemoryMb = env.MemoryMb,
        TunnelProvider = env.TunnelProvider?.ToString(),
        PortMappings = env.PortMappings is { Count: > 0 } ? PortMapping.Format(env.PortMappings) : null,
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
        Name = Name,
        Status = Enum.Parse<EnvironmentStatus>(Status),
        TtlMinutes = TtlMinutes,
        CreatedAt = CreatedAt,
        CpuCores = CpuCores,
        MemoryMb = MemoryMb,
        TunnelProvider = TunnelProvider is null ? null : Enum.Parse<Core.Models.TunnelProvider>(TunnelProvider),
        PortMappings = string.IsNullOrEmpty(PortMappings) ? null : PortMapping.ParseList(PortMappings),
        LastActivityAt = LastActivityAt,
        PublicUrl = PublicUrl,
        AccessToken = AccessToken,
        TunnelName = TunnelName,
        TunnelReady = TunnelReady,
    };
}
