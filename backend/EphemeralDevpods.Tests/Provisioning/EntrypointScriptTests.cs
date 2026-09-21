using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Provisioning;

namespace EphemeralDevpods.Tests.Provisioning;

public class EntrypointScriptTests
{
    private static string Build(
        string[]? postCreate = null, string[]? postAttach = null, TunnelProvider provider = TunnelProvider.GitHub) =>
        EntrypointScript.Build(
            "https://github.com/owner/repo", postCreate ?? [], postAttach ?? [], "epd-12345678", provider);

    [Theory]
    [InlineData(TunnelProvider.GitHub, "--provider github")]
    [InlineData(TunnelProvider.Microsoft, "--provider microsoft")]
    public void Logs_in_with_the_selected_provider(TunnelProvider provider, string expected)
    {
        Assert.Contains(expected, Build(provider: provider));
    }

    [Fact]
    public void Clones_and_runs_post_create_only_until_setup_has_completed_once()
    {
        var script = Build(postCreate: ["npm install"]);

        Assert.Contains("if [ ! -f /tmp/.epd-setup-done ]; then", script);
        Assert.Contains("[ -d /workspace/.git ] || git clone 'https://github.com/owner/repo' /workspace", script);
        Assert.Contains("(cd /workspace && npm install) && touch /tmp/.epd-setup-done || exit 1; fi", script);
    }

    [Fact]
    public void Setup_marker_is_written_even_without_a_post_create_command()
    {
        Assert.Contains("git clone 'https://github.com/owner/repo' /workspace; } && touch /tmp/.epd-setup-done", Build());
    }

    [Fact]
    public void Installs_the_code_cli_only_when_it_is_missing()
    {
        Assert.Contains("[ -x /opt/ephemeral-devpods-vscode-cli/code ] || (", Build());
    }

    [Fact]
    public void Always_returns_to_the_workspace_and_starts_the_tunnel()
    {
        var script = Build(postAttach: ["npm start"]);

        Assert.Contains("cd /workspace && (npm start &)", script);
        Assert.Contains("exec /opt/ephemeral-devpods-vscode-cli/code tunnel --name 'epd-12345678'", script);
    }

    [Fact]
    public void Array_form_commands_are_quoted_token_by_token()
    {
        var script = Build(postCreate: ["echo", "hello world"]);

        Assert.Contains("'echo' 'hello world'", script);
    }

    [Fact]
    public void Keeps_the_tunnel_as_the_main_process_by_default()
    {
        var script = Build();

        Assert.Contains("exec /opt/ephemeral-devpods-vscode-cli/code tunnel", script);
        Assert.DoesNotContain("trap ", script);
    }

    [Fact]
    public void Unregister_variant_runs_the_tunnel_as_a_child_and_unregisters_on_sigterm()
    {
        var script = EntrypointScript.Build(
            "https://github.com/owner/repo", [], [], "epd-12345678", TunnelProvider.GitHub, unregisterOnTerminate: true);

        Assert.DoesNotContain("exec /opt/ephemeral-devpods-vscode-cli/code tunnel", script);
        Assert.Contains(
            "{ /opt/ephemeral-devpods-vscode-cli/code tunnel --name 'epd-12345678' --accept-server-license-terms & _tunnel_pid=$!;",
            script);
        Assert.Contains("trap '/opt/ephemeral-devpods-vscode-cli/code tunnel unregister", script);
        Assert.Contains("TERM INT; wait $_tunnel_pid; }", script);
    }
}
