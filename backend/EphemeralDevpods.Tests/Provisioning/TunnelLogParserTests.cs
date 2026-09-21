using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;

namespace EphemeralDevpods.Tests.Provisioning;

public class TunnelLogParserTests
{
    // Prompt line captured from a real `code tunnel user login --provider github` run (CLI 1.138.0).
    private const string PromptLine =
        "To grant access to the server, please log into https://github.com/login/device and use code 63B6-E507";

    private const string LoginRequired = TunnelLogParser.LoginRequiredMarker;
    private const string Starting = TunnelLogParser.StartingMarker;

    [Fact]
    public void Awaiting_login_exposes_the_device_code_and_url()
    {
        var state = TunnelLogParser.Parse($"Cloning into '/workspace'...\n{LoginRequired}\n{PromptLine}\n");

        Assert.Equal(TunnelPhase.AwaitingLogin, state.Phase);
        Assert.Equal("63B6-E507", state.DeviceCode);
        Assert.Equal("https://github.com/login/device", state.VerificationUrl);
    }

    [Fact]
    public void Newest_code_wins_after_an_expired_one_is_retried()
    {
        var retried = PromptLine.Replace("63B6-E507", "AAAA-1111");

        var state = TunnelLogParser.Parse($"{LoginRequired}\n{PromptLine}\n{LoginRequired}\n{retried}\n");

        Assert.Equal(TunnelPhase.AwaitingLogin, state.Phase);
        Assert.Equal("AAAA-1111", state.DeviceCode);
    }

    [Fact]
    public void Starting_marker_after_the_prompt_means_login_is_done()
    {
        var state = TunnelLogParser.Parse($"{LoginRequired}\n{PromptLine}\n{Starting}\n");

        Assert.Equal(TunnelPhase.Starting, state.Phase);
        Assert.Null(state.DeviceCode);
    }

    [Fact]
    public void No_login_marker_means_the_tunnel_is_just_starting()
    {
        Assert.Equal(TunnelPhase.Starting, TunnelLogParser.Parse($"{Starting}\n").Phase);
        Assert.Equal(TunnelPhase.Starting, TunnelLogParser.Parse("").Phase);
    }

    [Fact]
    public void Login_marker_without_a_prompt_yet_is_still_starting()
    {
        var state = TunnelLogParser.Parse($"{LoginRequired}\n");

        Assert.Equal(TunnelPhase.Starting, state.Phase);
        Assert.Null(state.DeviceCode);
    }

    [Fact]
    public void A_prompt_from_before_the_latest_marker_is_ignored()
    {
        var state = TunnelLogParser.Parse($"{PromptLine}\n{LoginRequired}\n");

        Assert.Equal(TunnelPhase.Starting, state.Phase);
    }

    [Theory]
    [InlineData("{\"tunnel\":null,\"service_installed\":false}", false)]
    [InlineData("{\"tunnel\":{\"name\":\"epd-1234abcd\"},\"service_installed\":false}", true)]
    [InlineData("", false)]
    [InlineData("sh: /opt/ephemeral-devpods-vscode-cli/code: not found", false)]
    [InlineData("[1,2,3]", false)]
    public void IsConnected_reads_the_status_json(string statusJson, bool expected) =>
        Assert.Equal(expected, TunnelLogParser.IsConnected(statusJson));
}
