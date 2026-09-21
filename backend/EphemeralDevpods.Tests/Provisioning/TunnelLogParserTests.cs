using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning;

namespace EphemeralDevpods.Tests.Provisioning;

public class TunnelLogParserTests
{
    // Prompt line captured from a real `code tunnel user login --provider github` run (CLI 1.138.0).
    private const string PromptLine =
        "To grant access to the server, please log into https://github.com/login/device and use code 63B6-E507";

    private const string LoginRequired = TunnelLogParser.LoginRequiredMarker;

    private const string Starting = TunnelLogParser.StartingMarker;

    [Fact]
    public void Connected_when_the_tunnel_link_is_printed_after_the_starting_marker()
    {
        var logs = $"{Starting}\nOpen this link in your browser https://vscode.dev/tunnel/epd-12345678/workspace";

        Assert.True(TunnelLogParser.IsConnectedInLogs(logs, "epd-12345678"));
    }

    [Fact]
    public void Not_connected_before_the_link_appears()
    {
        Assert.False(TunnelLogParser.IsConnectedInLogs($"{Starting}\nConnecting to tunnel service", "epd-12345678"));
    }

    [Fact]
    public void A_link_from_before_the_latest_restart_does_not_count()
    {
        var logs = $"{Starting}\nhttps://vscode.dev/tunnel/epd-12345678/workspace\n{LoginRequired}\n{Starting}\nstarting again";

        Assert.False(TunnelLogParser.IsConnectedInLogs(logs, "epd-12345678"));
    }

    [Fact]
    public void Another_tunnels_link_does_not_count()
    {
        Assert.False(TunnelLogParser.IsConnectedInLogs(
            $"{Starting}\nhttps://vscode.dev/tunnel/epd-99999999/workspace", "epd-12345678"));
    }

    [Fact]
    public void Not_connected_without_the_starting_marker()
    {
        Assert.False(TunnelLogParser.IsConnectedInLogs("https://vscode.dev/tunnel/epd-12345678/workspace", "epd-12345678"));
    }
}
