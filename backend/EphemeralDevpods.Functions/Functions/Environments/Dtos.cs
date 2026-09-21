using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>Everything but <c>RepoUrl</c> is optional; a missing option falls back to its default.</summary>
public sealed record CreateEnvironmentRequest(
    string? RepoUrl, string? Name = null, int? TtlMinutes = null, int? CpuCores = null, int? MemoryMb = null,
    string? TunnelProvider = null, IReadOnlyList<PortMapping>? PortMappings = null);

public sealed record CheckRepositoryRequest(string? RepoUrl);

/// <summary>
/// What step 1 of the create flow shows once a repo checks out. <c>PortMappings</c> are the default host ports
/// for the repo's forwarded ports; when <c>HostPortsSelectable</c> (local Docker) the user may change them on
/// step 2 and send their choice back on create.
/// </summary>
public sealed record CheckRepositoryResponse(
    string RepoUrl, string Owner, string Repo, string SuggestedName, string? Image,
    IReadOnlyList<PortMapping> PortMappings, bool HostPortsSelectable)
{
    public static CheckRepositoryResponse From(
        string repoUrl, RepoInspection inspection, IReadOnlyList<PortMapping> portMappings, bool hostPortsSelectable) => new(
        repoUrl, inspection.Owner, inspection.Repo, inspection.SuggestedName, inspection.Spec.Image,
        portMappings, hostPortsSelectable);
}

/// <summary>
/// The VS Code tunnel as the dashboard needs it. The device code is only meaningful (and only returned)
/// while <c>Phase</c> is <c>AwaitingLogin</c>; the editor URLs only once it's <c>Ready</c>.
/// </summary>
public sealed record TunnelResponse(
    string Phase, string? DeviceCode, string? VerificationUrl, string? WebEditorUrl, string? LocalEditorUrl)
{
    // The repo is cloned to /workspace inside the container (LocalDockerProvisioner's entrypoint).
    private const string WorkspaceFolder = "workspace";

    public static TunnelResponse? From(WorkspaceEnvironment env, TunnelState? live)
    {
        if (env.TunnelName is null || env.Status != EnvironmentStatus.Running)
        {
            return null;
        }

        var state = env.TunnelReady ? new TunnelState(TunnelPhase.Ready) : live ?? new TunnelState(TunnelPhase.Starting);
        return state.Phase switch
        {
            TunnelPhase.Ready => new TunnelResponse(
                nameof(TunnelPhase.Ready), null, null,
                $"https://vscode.dev/tunnel/{env.TunnelName}/{WorkspaceFolder}",
                $"vscode://vscode-remote/tunnel+{env.TunnelName}/{WorkspaceFolder}"),
            TunnelPhase.AwaitingLogin => new TunnelResponse(
                nameof(TunnelPhase.AwaitingLogin), state.DeviceCode, state.VerificationUrl, null, null),
            _ => new TunnelResponse(nameof(TunnelPhase.Starting), null, null, null, null),
        };
    }
}

/// <summary>What the frontend's WorkspaceEnvironment TS type expects — deliberately excludes
/// AccessToken (which the container itself uses, not the dashboard) and Owner (an internal user id
/// the caller has no use for).</summary>
public sealed record EnvironmentResponse(
    string EnvironmentId, string RepoUrl, string? Name, string Status, int TtlMinutes,
    DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt, int? CpuCores, int? MemoryMb, string TunnelProvider,
    IReadOnlyList<PortMapping> PortMappings, string? PublicUrl, TunnelResponse? Tunnel)
{
    public static EnvironmentResponse From(WorkspaceEnvironment env, TunnelState? tunnel = null) => new(
        env.EnvironmentId, env.RepoUrl, env.Name, env.Status.ToString(), env.TtlMinutes, env.CreatedAt,
        env.CreatedAt.AddMinutes(env.TtlMinutes), env.CpuCores, env.MemoryMb,
        // Environments from before the provider was selectable always logged in with GitHub.
        (env.TunnelProvider ?? Core.Models.TunnelProvider.GitHub).ToString(),
        env.PortMappings ?? [], env.PublicUrl, TunnelResponse.From(env, tunnel));
}
