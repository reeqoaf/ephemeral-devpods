using System.Text;
using Docker.DotNet;
using Docker.DotNet.Models;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;

namespace EphemeralDevpods.Infrastructure.Provisioning.LocalDocker;

/// <summary>
/// Local-dev stand-in for AciProvisioner: runs the environment as a real Docker container via
/// Docker.DotNet (docs/spec.md §13). One container per environment, ports published on
/// localhost, no access token needed since nothing leaves the machine.
/// </summary>
public sealed class LocalDockerProvisioner(IDockerClient dockerClient, IBuildContextFetcher buildContextFetcher)
    : IComputeProvisioner
{
    private const string EnvironmentIdLabel = "ephemeral-devpods.environmentId";

    public async Task<ProvisionResult> ProvisionAsync(
        WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        var imageRef = await ResolveImageAsync(environment, spec, ct);
        var tunnelName = TunnelNameFor(environment.EnvironmentId);
        var entrypointScript = BuildEntrypointScript(
            environment.RepoUrl, spec.PostCreateCommand, spec.PostAttachCommand, tunnelName);

        var exposedPorts = new Dictionary<string, EmptyStruct>();
        var portBindings = new Dictionary<string, IList<PortBinding>>();
        foreach (var port in spec.ForwardPorts)
        {
            var key = $"{port}/tcp";
            exposedPorts[key] = new EmptyStruct();
            portBindings[key] = [new PortBinding { HostPort = port.ToString() }];
        }

        var createResponse = await dockerClient.Containers.CreateContainerAsync(
            new CreateContainerParameters
            {
                Name = $"ephemeral-devpods-{environment.EnvironmentId}",
                Image = imageRef,
                Env = spec.ContainerEnv.Select(kv => $"{kv.Key}={kv.Value}").ToList(),
                Entrypoint = ["sh", "-c"],
                Cmd = [entrypointScript],
                ExposedPorts = exposedPorts,
                Labels = new Dictionary<string, string> { [EnvironmentIdLabel] = environment.EnvironmentId },
                HostConfig = new HostConfig { PortBindings = portBindings },
            },
            ct);

        await dockerClient.Containers.StartContainerAsync(createResponse.ID, new ContainerStartParameters(), ct);

        var resource = new DeployedResource
        {
            EnvironmentId = environment.EnvironmentId,
            ResourceId = createResponse.ID,
            Type = ResourceType.Container,
            ProviderResourceId = createResponse.ID,
            Image = imageRef,
            Port = spec.ForwardPorts.Count > 0 ? spec.ForwardPorts[0] : null,
            Status = "Running",
        };

        return new ProvisionResult
        {
            PublicUrl = spec.ForwardPorts.Count > 0 ? $"http://localhost:{spec.ForwardPorts[0]}" : "",
            AccessToken = "", // purely localhost-bound — no token needed, nothing is internet-reachable
            TunnelName = tunnelName,
            Resources = [resource],
        };
    }

    public async Task TeardownAsync(string environmentId, CancellationToken ct)
    {
        foreach (var container in await FindContainersAsync(environmentId, ct))
        {
            await UnregisterTunnelAsync(container, ct);
            await dockerClient.Containers.StopContainerAsync(container.ID, new ContainerStopParameters(), ct);
            await dockerClient.Containers.RemoveContainerAsync(
                container.ID, new ContainerRemoveParameters { Force = true }, ct);
        }
    }

    public async Task<EnvironmentStatus> GetStatusAsync(string environmentId, CancellationToken ct)
    {
        var container = (await FindContainersAsync(environmentId, ct)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"No local Docker container found for environment {environmentId}.");

        return container.State switch
        {
            "running" => EnvironmentStatus.Running,
            "created" or "restarting" => EnvironmentStatus.Provisioning,
            _ => EnvironmentStatus.Failed,
        };
    }

    public async Task<TunnelState> GetTunnelStateAsync(string environmentId, CancellationToken ct)
    {
        var container = (await FindContainersAsync(environmentId, ct)).FirstOrDefault()
            ?? throw new KeyNotFoundException($"No local Docker container found for environment {environmentId}.");

        using var logs = await dockerClient.Containers.GetContainerLogsAsync(
            container.ID,
            tty: false,
            new ContainerLogsParameters { ShowStdout = true, ShowStderr = true, Tail = "200" },
            ct);
        var (stdout, stderr) = await logs.ReadOutputToEndAsync(ct);

        var fromLogs = TunnelLogParser.Parse(stdout + stderr);
        if (fromLogs.Phase == TunnelPhase.AwaitingLogin)
        {
            return fromLogs;
        }

        var status = await ExecAsync(container.ID, [CodeCliPath, "tunnel", "status"], ct);
        return status is not null && TunnelLogParser.IsConnected(status)
            ? new TunnelState(TunnelPhase.Ready)
            : fromLogs;
    }

    /// <summary>
    /// Best effort: a tunnel left registered keeps counting against the user's per-account tunnel limit
    /// after its container is gone. Teardown must never fail because of this.
    /// </summary>
    private async Task UnregisterTunnelAsync(ContainerListResponse container, CancellationToken ct)
    {
        if (container.State != "running")
        {
            return;
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            await ExecAsync(container.ID, [CodeCliPath, "tunnel", "unregister"], timeout.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // unregister hung (e.g. waiting on a login) — carry on with the teardown
        }
    }

    /// <summary>Runs a command in the container; null if it couldn't be started (container stopping, CLI missing).</summary>
    private async Task<string?> ExecAsync(string containerId, string[] command, CancellationToken ct)
    {
        try
        {
            var exec = await dockerClient.Exec.ExecCreateContainerAsync(
                containerId,
                new ContainerExecCreateParameters { AttachStdout = true, AttachStderr = true, Cmd = command },
                ct);
            using var stream = await dockerClient.Exec.StartAndAttachContainerExecAsync(exec.ID, tty: false, ct);
            var (stdout, _) = await stream.ReadOutputToEndAsync(ct);
            return stdout;
        }
        catch (DockerApiException)
        {
            return null;
        }
    }

    /// <summary>Short and stable so it fits in the vscode.dev URL; the 8-char prefix of the GUID is unique enough per account.</summary>
    private static string TunnelNameFor(string environmentId) => $"epd-{environmentId[..8]}";

    private Task<IList<ContainerListResponse>> FindContainersAsync(string environmentId, CancellationToken ct) =>
        dockerClient.Containers.ListContainersAsync(
            new ContainersListParameters
            {
                All = true,
                Filters = new Dictionary<string, IDictionary<string, bool>>
                {
                    ["label"] = new Dictionary<string, bool> { [$"{EnvironmentIdLabel}={environmentId}"] = true },
                },
            },
            ct);

    private async Task<string> ResolveImageAsync(WorkspaceEnvironment environment, EnvironmentSpec spec, CancellationToken ct)
    {
        if (spec.DockerfilePath is null)
        {
            await PullImageAsync(spec.Image!, ct);
            return spec.Image!;
        }

        var buildContext = await buildContextFetcher.FetchAsync(
            environment.RepoUrl, spec.DevcontainerBaseDirectory, spec.BuildContextPath ?? ".", spec.DockerfilePath, ct);

        await using (buildContext.Tar)
        {
            var tag = $"ephemeral-devpods/{environment.EnvironmentId}:latest";
            string? buildError = null;

            var progress = new Progress<JSONMessage>(message =>
            {
                if (!string.IsNullOrEmpty(message.ErrorMessage))
                {
                    buildError = message.ErrorMessage;
                }
            });

            await dockerClient.Images.BuildImageFromDockerfileAsync(
                new ImageBuildParameters
                {
                    Dockerfile = buildContext.DockerfilePathInContext,
                    Tags = [tag],
                    BuildArgs = spec.BuildArgs.Count > 0 ? new Dictionary<string, string>(spec.BuildArgs) : null,
                },
                buildContext.Tar,
                authConfigs: [],
                headers: new Dictionary<string, string>(),
                progress,
                ct);

            if (buildError is not null)
            {
                throw new InvalidOperationException($"Docker build failed: {buildError}");
            }

            return tag;
        }
    }

    private async Task PullImageAsync(string image, CancellationToken ct)
    {
        var (fromImage, tag) = SplitImageTag(image);
        await dockerClient.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = fromImage, Tag = tag },
            authConfig: null,
            new Progress<JSONMessage>(),
            ct);
    }

    /// <summary>Splits "repo:tag" — careful not to mistake a registry port for a tag separator.</summary>
    private static (string FromImage, string Tag) SplitImageTag(string image)
    {
        var lastColon = image.LastIndexOf(':');
        return lastColon > image.LastIndexOf('/')
            ? (image[..lastColon], image[(lastColon + 1)..])
            : (image, "latest");
    }

    /// <summary>
    /// Always downloads the standalone VS Code CLI fresh and invokes it by absolute path,
    /// deliberately bypassing PATH — devcontainer base images often already have a placeholder
    /// `code` script on PATH that reports "not installed" until the real server connects, so a
    /// `command -v code` check finds that stub instead of the real thing. `cli-alpine-<arch>` is
    /// the musl build; it's Microsoft's own recommendation for containers and runs fine on
    /// glibc images too. Requires curl + tar in the base image (true of the standard ones).
    /// </summary>
    private const string InstallCodeCliFragment =
        "mkdir -p /opt/ephemeral-devpods-vscode-cli && " +
        "case \"$(uname -m)\" in " +
        "aarch64|arm64) _cli_arch=arm64 ;; " +
        "*) _cli_arch=x64 ;; " + // no 32-bit ARM build is published
        "esac && " +
        "curl -Ls \"https://code.visualstudio.com/sha/download?build=stable&os=cli-alpine-$_cli_arch\" " +
        "-o /tmp/vscode_cli.tar.gz && " +
        "tar -xf /tmp/vscode_cli.tar.gz -C /opt/ephemeral-devpods-vscode-cli";

    private const string CodeCliPath = "/opt/ephemeral-devpods-vscode-cli/code";

    /// <summary>
    /// Login is its own step before the tunnel: chaining `user login` inside `code tunnel` makes the CLI
    /// exit right after authenticating. The loop re-prompts (with a fresh device code) if a code expires,
    /// and the marker lines let <see cref="TunnelLogParser"/> tell "waiting for login" from "starting".
    /// </summary>
    private static string BuildTunnelFragment(string tunnelName) =>
        $"until {CodeCliPath} tunnel user show >/dev/null 2>&1; do " +
        $"echo {ShellQuote(TunnelLogParser.LoginRequiredMarker)}; " +
        $"{CodeCliPath} tunnel user login --provider github || sleep 2; " +
        "done && " +
        $"echo {ShellQuote(TunnelLogParser.StartingMarker)} && " +
        $"exec {CodeCliPath} tunnel --name {ShellQuote(tunnelName)} --accept-server-license-terms";

    private static string BuildEntrypointScript(
        string repoUrl, IReadOnlyList<string> postCreateCommand, IReadOnlyList<string> postAttachCommand,
        string tunnelName)
    {
        var script = new StringBuilder("set -e; ")
            .Append($"git clone {ShellQuote(repoUrl)} /workspace && cd /workspace");

        var postCreateFragment = BuildShellFragment(postCreateCommand);
        if (postCreateFragment.Length > 0)
        {
            script.Append(" && ").Append(postCreateFragment);
        }

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
            .Append(" && ").Append(BuildTunnelFragment(tunnelName))
            .ToString();
    }

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
