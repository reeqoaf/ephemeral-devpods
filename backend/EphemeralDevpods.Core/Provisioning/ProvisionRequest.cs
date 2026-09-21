using System.Text.Json;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Core.Provisioning;

/// <summary>
/// The work item handed from CreateEnvironment to the provisioning worker. It carries the parsed spec so the worker
/// doesn't re-fetch devcontainer.json (the repo could have changed between accepting the request and running it).
/// </summary>
public sealed record ProvisionRequest(string Owner, string EnvironmentId, EnvironmentSpec Spec)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string ToMessage() => JsonSerializer.Serialize(this, Json);

    /// <exception cref="JsonException">The message isn't a provision request.</exception>
    public static ProvisionRequest FromMessage(string message) =>
        JsonSerializer.Deserialize<ProvisionRequest>(message, Json) ?? throw new JsonException("Empty provision message.");
}

/// <summary>
/// Provisioning outlasts an HTTP request (image pulls, ACR builds), so creates are queued and run by a worker
/// while the environment row sits in <see cref="EnvironmentStatus.Provisioning"/>.
/// </summary>
public interface IProvisioningQueue
{
    Task EnqueueAsync(ProvisionRequest request, CancellationToken ct);
}
