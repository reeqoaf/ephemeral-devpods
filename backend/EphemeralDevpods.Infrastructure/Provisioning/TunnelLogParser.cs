using System.Text.Json;
using System.Text.RegularExpressions;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Infrastructure.Provisioning;

/// <summary>
/// Derives tunnel state from a container's log output and from `code tunnel status`. The entrypoint
/// prints its own marker lines around the login and tunnel steps, so only the device-code line depends
/// on the VS Code CLI's wording ("To grant access to the server, please log into &lt;url&gt; and use code &lt;code&gt;").
/// </summary>
public static partial class TunnelLogParser
{
    public const string LoginRequiredMarker = "[epd] tunnel-login-required";
    public const string StartingMarker = "[epd] tunnel-starting";

    [GeneratedRegex(@"please log into (?<url>\S+) and use code (?<code>[A-Za-z0-9]{4}-[A-Za-z0-9]{4})")]
    private static partial Regex DeviceCodeLine();

    /// <summary>
    /// Only ever returns <see cref="TunnelPhase.AwaitingLogin"/> or <see cref="TunnelPhase.Starting"/> —
    /// whether the tunnel is actually connected is answered by <see cref="IsConnected"/>.
    /// </summary>
    public static TunnelState Parse(string logs)
    {
        var loginRequiredAt = logs.LastIndexOf(LoginRequiredMarker, StringComparison.Ordinal);
        var startingAt = logs.LastIndexOf(StartingMarker, StringComparison.Ordinal);

        // Login finished after the last prompt (or no login was ever needed): the tunnel itself is starting.
        if (loginRequiredAt < 0 || startingAt > loginRequiredAt)
        {
            return new TunnelState(TunnelPhase.Starting);
        }

        // A code can expire and the entrypoint retries, so the newest prompt after the marker is the live one.
        var match = DeviceCodeLine().Matches(logs, loginRequiredAt).LastOrDefault();
        return match is null
            ? new TunnelState(TunnelPhase.Starting)
            : new TunnelState(TunnelPhase.AwaitingLogin, match.Groups["code"].Value, match.Groups["url"].Value);
    }

    /// <summary>
    /// For backends that can't exec `code tunnel status` (ACI exec is one argument-less process): the CLI prints the
    /// tunnel's vscode.dev link once it is registered and connected. Only counts output after the entrypoint's own
    /// "tunnel starting" marker, so a link from before a restart doesn't count.
    /// </summary>
    public static bool IsConnectedInLogs(string logs, string tunnelName)
    {
        var startingAt = logs.LastIndexOf(StartingMarker, StringComparison.Ordinal);
        return startingAt >= 0
            && logs.IndexOf($"vscode.dev/tunnel/{tunnelName}", startingAt, StringComparison.Ordinal) >= 0;
    }

    /// <summary>`code tunnel status` prints `{"tunnel":null,...}` until a tunnel is registered.</summary>
    public static bool IsConnected(string statusJson)
    {
        try
        {
            using var document = JsonDocument.Parse(statusJson);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("tunnel", out var tunnel)
                && tunnel.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false; // the CLI isn't installed yet, or printed an error instead of JSON
        }
    }
}
