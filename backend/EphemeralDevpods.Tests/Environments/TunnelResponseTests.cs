using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Functions.Functions.Environments;

namespace EphemeralDevpods.Tests.Environments;

public class TunnelResponseTests
{
    private static WorkspaceEnvironment MakeEnvironment(
        EnvironmentStatus status = EnvironmentStatus.Running, string? tunnelName = "epd-12345678", bool tunnelReady = false) => new()
    {
        Owner = "owner",
        EnvironmentId = Guid.NewGuid().ToString(),
        RepoUrl = "https://github.com/owner/repo",
        Status = status,
        TtlMinutes = 60,
        CreatedAt = DateTimeOffset.UtcNow,
        TunnelName = tunnelName,
        TunnelReady = tunnelReady,
    };

    [Fact]
    public void Ready_builds_both_editor_urls_and_hides_the_device_code()
    {
        var response = TunnelResponse.From(MakeEnvironment(tunnelReady: true), null);

        Assert.NotNull(response);
        Assert.Equal("Ready", response.Phase);
        Assert.Equal("https://vscode.dev/tunnel/epd-12345678/workspace", response.WebEditorUrl);
        Assert.Equal("vscode://vscode-remote/tunnel+epd-12345678/workspace", response.LocalEditorUrl);
        Assert.Null(response.DeviceCode);
    }

    [Fact]
    public void Awaiting_login_carries_the_code_but_no_editor_urls()
    {
        var live = new TunnelState(TunnelPhase.AwaitingLogin, "AAAA-1111", "https://github.com/login/device");

        var response = TunnelResponse.From(MakeEnvironment(), live);

        Assert.NotNull(response);
        Assert.Equal("AwaitingLogin", response.Phase);
        Assert.Equal("AAAA-1111", response.DeviceCode);
        Assert.Equal("https://github.com/login/device", response.VerificationUrl);
        Assert.Null(response.WebEditorUrl);
        Assert.Null(response.LocalEditorUrl);
    }

    [Fact]
    public void Without_live_state_a_running_environment_is_starting()
    {
        Assert.Equal("Starting", TunnelResponse.From(MakeEnvironment(), null)?.Phase);
    }

    [Fact]
    public void The_persisted_ready_flag_wins_over_a_stale_live_state()
    {
        var response = TunnelResponse.From(MakeEnvironment(tunnelReady: true), new TunnelState(TunnelPhase.Starting));

        Assert.Equal("Ready", response?.Phase);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Provisioning)]
    [InlineData(EnvironmentStatus.Expired)]
    [InlineData(EnvironmentStatus.Failed)]
    public void No_tunnel_unless_the_environment_is_running(EnvironmentStatus status) =>
        Assert.Null(TunnelResponse.From(MakeEnvironment(status, tunnelReady: true), null));

    [Fact]
    public void No_tunnel_for_environments_created_before_tunnels_were_named() =>
        Assert.Null(TunnelResponse.From(MakeEnvironment(tunnelName: null), null));
}
