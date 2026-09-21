using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Functions.Functions.Environments;

public sealed record CreateEnvironmentRequest(string? RepoUrl);

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
    string EnvironmentId, string RepoUrl, string Status, int TtlMinutes,
    DateTimeOffset CreatedAt, string? PublicUrl, TunnelResponse? Tunnel)
{
    public static EnvironmentResponse From(WorkspaceEnvironment env, TunnelState? tunnel = null) => new(
        env.EnvironmentId, env.RepoUrl, env.Status.ToString(), env.TtlMinutes, env.CreatedAt, env.PublicUrl,
        TunnelResponse.From(env, tunnel));
}
