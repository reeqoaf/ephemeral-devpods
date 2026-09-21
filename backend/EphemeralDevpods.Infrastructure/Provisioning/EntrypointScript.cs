using System.Text;
using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Provisioning;

/// <summary>
/// Builds the shell script a container runs as its entrypoint: clone the repo, run the devcontainer's
/// lifecycle commands, install the VS Code CLI, log in and start the tunnel. The script runs again on
/// every container start or restart, so each step that has lasting effects is guarded to be re-runnable.
/// </summary>
public static class EntrypointScript
{
    private const string CodeCliDirectory = "/opt/ephemeral-devpods-vscode-cli";
    public const string CodeCliPath = CodeCliDirectory + "/code";

    private const string WorkspaceDirectory = "/workspace";
    private const string SetupDoneMarker = "/tmp/.epd-setup-done";

    /// <summary>
    /// Downloads the standalone VS Code CLI once and invokes it by absolute path, deliberately bypassing
    /// PATH — devcontainer base images often already have a placeholder `code` script on PATH that reports
    /// "not installed" until the real server connects, so a `command -v code` check finds that stub instead
    /// of the real thing. `cli-alpine-&lt;arch&gt;` is the musl build; it's Microsoft's own recommendation for
    /// containers and runs fine on glibc images too. Requires curl + tar in the base image (true of the
    /// standard ones). Skipped on later runs because the CLI survives a container stop/start.
    /// </summary>
    private const string InstallCodeCliFragment =
        "([ -x " + CodeCliPath + " ] || (" +
        "mkdir -p " + CodeCliDirectory + " && " +
        "case \"$(uname -m)\" in " +
        "aarch64|arm64) _cli_arch=arm64 ;; " +
        "*) _cli_arch=x64 ;; " + // no 32-bit ARM build is published
        "esac && " +
        "curl -Ls \"https://code.visualstudio.com/sha/download?build=stable&os=cli-alpine-$_cli_arch\" " +
        "-o /tmp/vscode_cli.tar.gz && " +
        "tar -xf /tmp/vscode_cli.tar.gz -C " + CodeCliDirectory + "))";

    /// <param name="unregisterOnTerminate">
    /// Runs the tunnel as a child instead of `exec`ing it, so the shell survives to catch SIGTERM and unregister the
    /// tunnel first. For backends that can't exec `tunnel unregister` into the container on stop/delete (ACI); the
    /// default keeps the tunnel as the container's main process.
    /// </param>
    public static string Build(
        string repoUrl, IReadOnlyList<string> postCreateCommand, IReadOnlyList<string> postAttachCommand,
        string tunnelName, TunnelProvider tunnelProvider, bool unregisterOnTerminate = false)
    {
        // Clone + postCreateCommand are first-run setup. The marker is only written once both succeeded, so
        // a start after a failed setup retries it instead of launching a half-built workspace; a failed
        // setup aborts the script.
        var setup = new StringBuilder(
            $"{{ [ -d {WorkspaceDirectory}/.git ] || git clone {ShellQuote(repoUrl)} {WorkspaceDirectory}; }}");
        var postCreateFragment = BuildShellFragment(postCreateCommand);
        if (postCreateFragment.Length > 0)
        {
            setup.Append($" && (cd {WorkspaceDirectory} && {postCreateFragment})");
        }

        setup.Append($" && touch {SetupDoneMarker}");

        var script = new StringBuilder("set -e; ")
            .Append($"if [ ! -f {SetupDoneMarker} ]; then {setup} || exit 1; fi; ")
            .Append($"cd {WorkspaceDirectory}");

        // Backgrounded (never blocks the chain) — this is where devcontainer.json conventionally
        // puts a dev-server start command, since postCreateCommand is meant to finish (setup) and
        // the container's foreground/main process needs to stay the VS Code tunnel below.
        var postAttachFragment = BuildShellFragment(postAttachCommand);
        if (postAttachFragment.Length > 0)
        {
            script.Append(" && (").Append(postAttachFragment).Append(" &)");
        }

        return script
            .Append(" && ").Append(InstallCodeCliFragment)
            .Append(" && ").Append(BuildTunnelFragment(tunnelName, tunnelProvider, unregisterOnTerminate))
            .ToString();
    }

    /// <summary>
    /// Login is its own step before the tunnel: chaining `user login` inside `code tunnel` makes the CLI
    /// exit right after authenticating. The loop re-prompts (with a fresh device code) if a code expires,
    /// and the marker lines let <see cref="TunnelLogParser"/> tell "waiting for login" from "starting".
    /// The login persists in the container, so a restart skips straight to the tunnel.
    /// </summary>
    private static string BuildTunnelFragment(string tunnelName, TunnelProvider tunnelProvider, bool unregisterOnTerminate) =>
        $"until {CodeCliPath} tunnel user show >/dev/null 2>&1; do " +
        $"echo {ShellQuote(TunnelLogParser.LoginRequiredMarker)}; " +
        $"{CodeCliPath} tunnel user login --provider {ProviderArgument(tunnelProvider)} || sleep 2; " +
        "done && " +
        $"echo {ShellQuote(TunnelLogParser.StartingMarker)} && " +
        (unregisterOnTerminate
            ? $"{{ {CodeCliPath} tunnel --name {ShellQuote(tunnelName)} --accept-server-license-terms & _tunnel_pid=$!; " +
              $"trap '{CodeCliPath} tunnel unregister >/dev/null 2>&1; kill $_tunnel_pid 2>/dev/null' TERM INT; " +
              "wait $_tunnel_pid; }"
            : $"exec {CodeCliPath} tunnel --name {ShellQuote(tunnelName)} --accept-server-license-terms");

    private static string ProviderArgument(TunnelProvider provider) => provider switch
    {
        TunnelProvider.GitHub => "github",
        TunnelProvider.Microsoft => "microsoft",
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    /// <summary>
    /// devcontainer.json's postCreateCommand/postAttachCommand string form is one raw shell
    /// command (interpreted as-authored); their array form is argv tokens for a single exec (no
    /// shell involved). Since both get spliced into one outer `sh -c` here, string form (always
    /// exactly 1 element) must stay unquoted so its own shell syntax still works, while array
    /// form's tokens each need quoting so embedded spaces don't get re-split.
    /// </summary>
    private static string BuildShellFragment(IReadOnlyList<string> command) =>
        command.Count switch
        {
            0 => "",
            1 => command[0],
            _ => string.Join(' ', command.Select(ShellQuote)),
        };

    private static string ShellQuote(string value) => $"'{value.Replace("'", "'\\''")}'";
}
