using Azure.ResourceManager.ContainerInstance.Models;
using Azure.ResourceManager.ContainerRegistry.Models;
using EphemeralDevpods.Core.Git;
using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Infrastructure.Provisioning;
using EphemeralDevpods.Infrastructure.Provisioning.Aci;

namespace EphemeralDevpods.Tests.Provisioning;

/// <summary>The parts of ACI provisioning that don't call Azure: what we deploy, what we build, how state maps.</summary>
public class AciProvisionerTests
{
    private const string EnvironmentId = "0f8fad5b-d9cb-469f-a165-70867728950e";

    private static AciOptions Options(bool pullCredentials = true) => new()
    {
        TenantId = "tenant",
        ClientId = "client",
        ClientSecret = "secret",
        SubscriptionId = "sub",
        ResourceGroup = "rg-envs",
        Location = "westeurope",
        RegistryName = "epdregistry",
        RegistryResourceGroup = "rg-platform",
        PullClientId = pullCredentials ? "pull-id" : null,
        PullClientSecret = pullCredentials ? "pull-secret" : null,
    };

    private static WorkspaceEnvironment Environment(int? cpu = 2, int? memoryMb = 4096) => new()
    {
        Owner = "owner-1",
        EnvironmentId = EnvironmentId,
        RepoUrl = "https://github.com/owner/repo",
        Status = EnvironmentStatus.Provisioning,
        TtlMinutes = 60,
        CreatedAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero),
        CpuCores = cpu,
        MemoryMb = memoryMb,
    };

    private static EnvironmentSpec ImageSpec(IReadOnlyDictionary<string, string>? env = null) => new()
    {
        Image = "node:22",
        ContainerEnv = env ?? new Dictionary<string, string>(),
    };

    [Fact]
    public void Group_is_named_after_the_environment()
    {
        Assert.Equal($"epd-{EnvironmentId}", AciGroupBuilder.GroupName(EnvironmentId));
    }

    [Fact]
    public void Group_runs_one_linux_container_with_the_entrypoint_and_the_requested_resources()
    {
        var group = AciGroupBuilder.Build(Options(), Environment(cpu: 2, memoryMb: 4096), ImageSpec(), "node:22", "echo hi");

        var container = Assert.Single(group.Containers);
        Assert.Equal("workspace", container.Name);
        Assert.Equal("node:22", container.Image);
        Assert.Equal(["sh", "-c", "echo hi"], container.Command);
        Assert.Equal(2, container.Resources.Requests.Cpu);
        Assert.Equal(4, container.Resources.Requests.MemoryInGB);
        Assert.Equal(ContainerInstanceOperatingSystemType.Linux, group.OSType);
        Assert.Equal("westeurope", group.Location.Name);
    }

    [Fact]
    public void Group_has_no_public_ip_or_ports_and_never_restarts()
    {
        var group = AciGroupBuilder.Build(Options(), Environment(), ImageSpec(), "node:22", "echo hi");

        Assert.Null(group.IPAddress); // reached only through the outbound VS Code tunnel
        Assert.Empty(group.Containers[0].Ports);
        Assert.Equal(ContainerGroupRestartPolicy.Never, group.RestartPolicy);
    }

    [Fact]
    public void Group_passes_container_env_through()
    {
        var group = AciGroupBuilder.Build(
            Options(), Environment(), ImageSpec(new Dictionary<string, string> { ["MODE"] = "dev" }), "node:22", "x");

        var variable = Assert.Single(group.Containers[0].EnvironmentVariables);
        Assert.Equal("MODE", variable.Name);
        Assert.Equal("dev", variable.Value);
    }

    [Fact]
    public void Environments_without_limits_get_what_is_offered_today()
    {
        var group = AciGroupBuilder.Build(Options(), Environment(cpu: null, memoryMb: null), ImageSpec(), "node:22", "x");

        Assert.Equal(EnvironmentOptions.AllowedCpuCores[0], group.Containers[0].Resources.Requests.Cpu);
        Assert.Equal(EnvironmentOptions.AllowedMemoryMb[0] / 1024.0, group.Containers[0].Resources.Requests.MemoryInGB);
    }

    [Fact]
    public void Group_is_tagged_for_ownership_and_cost_tracing()
    {
        var group = AciGroupBuilder.Build(Options(), Environment(), ImageSpec(), "node:22", "x");

        Assert.Equal(EnvironmentId, group.Tags["environmentId"]);
        Assert.Equal("owner-1", group.Tags["owner"]);
        Assert.Equal("https://github.com/owner/repo", group.Tags["repo"]);
        Assert.Equal("60", group.Tags["ttl"]);
        Assert.StartsWith("2026-09-22T10:00:00", group.Tags["createdAt"]);
    }

    [Fact]
    public void Overlong_repo_urls_are_truncated_to_the_tag_value_limit()
    {
        var environment = Environment();
        var longUrl = "https://github.com/owner/" + new string('r', 400);

        var group = AciGroupBuilder.Build(
            Options(), new WorkspaceEnvironment
            {
                Owner = environment.Owner,
                EnvironmentId = environment.EnvironmentId,
                RepoUrl = longUrl,
                Status = environment.Status,
                TtlMinutes = environment.TtlMinutes,
                CreatedAt = environment.CreatedAt,
            },
            ImageSpec(), "node:22", "x");

        Assert.Equal(256, group.Tags["repo"].Length);
    }

    [Fact]
    public void Registry_credentials_are_attached_only_for_images_from_our_registry()
    {
        var options = Options();
        var ours = AciGroupBuilder.Build(options, Environment(), ImageSpec(), AciBuild.ImageReference(options, EnvironmentId), "x");
        var publicImage = AciGroupBuilder.Build(options, Environment(), ImageSpec(), "node:22", "x");

        var credential = Assert.Single(ours.ImageRegistryCredentials);
        Assert.Equal("epdregistry.azurecr.io", credential.Server);
        Assert.Equal("pull-id", credential.Username);
        Assert.Empty(publicImage.ImageRegistryCredentials);
    }

    [Fact]
    public void Main_principals_secret_is_never_put_on_the_group()
    {
        var options = Options();
        var group = AciGroupBuilder.Build(options, Environment(), ImageSpec(), AciBuild.ImageReference(options, EnvironmentId), "x");

        Assert.DoesNotContain(group.ImageRegistryCredentials, c => c.Password == options.ClientSecret);
        Assert.DoesNotContain(group.Containers[0].EnvironmentVariables, v => v.Value == options.ClientSecret);
    }

    [Fact]
    public void Image_reference_points_into_the_registry_tagged_per_environment()
    {
        Assert.Equal(
            $"epdregistry.azurecr.io/epd/{EnvironmentId}:latest", AciBuild.ImageReference(Options(), EnvironmentId));
    }

    [Theory]
    [InlineData("", "https://github.com/owner/repo.git")]
    [InlineData("app", "https://github.com/owner/repo.git#:app")]
    [InlineData("src/app", "https://github.com/owner/repo.git#:src/app")]
    public void Source_location_is_the_git_url_with_the_context_folder(string context, string expected)
    {
        Assert.Equal(expected, AciBuild.SourceLocation("owner", "repo", context));
    }

    [Fact]
    public void Build_content_resolves_paths_relative_to_the_devcontainer_folder()
    {
        var spec = new EnvironmentSpec
        {
            DevcontainerBaseDirectory = ".devcontainer",
            BuildContextPath = "..",
            DockerfilePath = "Dockerfile",
            BuildArgs = new Dictionary<string, string> { ["NODE_VERSION"] = "22" },
        };

        var content = AciBuild.Content(EnvironmentId, "https://github.com/owner/repo", spec);

        Assert.Equal("https://github.com/owner/repo.git", content.SourceLocation); // ".." from .devcontainer is the repo root
        Assert.Equal(".devcontainer/Dockerfile", content.DockerFilePath);
        Assert.Equal([$"epd/{EnvironmentId}:latest"], content.ImageNames);
        Assert.True(content.IsPushEnabled);
        Assert.Equal(ContainerRegistryOSArchitecture.Amd64, content.Platform.Architecture);
        var argument = Assert.Single(content.Arguments);
        Assert.Equal(("NODE_VERSION", "22"), (argument.Name, argument.Value));
    }

    [Fact]
    public void Build_content_uses_a_subfolder_context_and_a_dockerfile_relative_to_it()
    {
        var spec = new EnvironmentSpec
        {
            DevcontainerBaseDirectory = ".devcontainer",
            BuildContextPath = "../app",
            DockerfilePath = "../app/docker/Dockerfile",
        };

        var content = AciBuild.Content(EnvironmentId, "https://github.com/owner/repo.git", spec);

        Assert.Equal("https://github.com/owner/repo.git#:app", content.SourceLocation);
        Assert.Equal("docker/Dockerfile", content.DockerFilePath);
    }

    [Fact]
    public void Build_content_rejects_a_dockerfile_outside_the_context()
    {
        var spec = new EnvironmentSpec
        {
            DevcontainerBaseDirectory = ".devcontainer",
            BuildContextPath = "../app",
            DockerfilePath = "Dockerfile", // .devcontainer/Dockerfile, not under app/
        };

        Assert.Throws<BuildContextNotFoundException>(
            () => AciBuild.Content(EnvironmentId, "https://github.com/owner/repo", spec));
    }

    [Theory]
    [InlineData("Succeeded", true)]
    [InlineData("Failed", true)]
    [InlineData("Canceled", true)]
    [InlineData("Error", true)]
    [InlineData("Timeout", true)]
    [InlineData("Queued", false)]
    [InlineData("Started", false)]
    [InlineData("Running", false)]
    public void Only_finished_runs_are_terminal(string status, bool expected)
    {
        Assert.Equal(expected, AciBuild.IsTerminal(new ContainerRegistryRunStatus(status)));
    }

    [Theory]
    [InlineData("Running", "Running", EnvironmentStatus.Running)]
    [InlineData("Succeeded", "Running", EnvironmentStatus.Running)]
    [InlineData("Creating", "Pending", EnvironmentStatus.Provisioning)]
    [InlineData("Creating", null, EnvironmentStatus.Provisioning)]
    [InlineData("Failed", null, EnvironmentStatus.Failed)]
    [InlineData("Succeeded", "Stopped", EnvironmentStatus.Stopped)]
    [InlineData("Succeeded", "Succeeded", EnvironmentStatus.Failed)] // the entrypoint exited
    [InlineData("Succeeded", "Failed", EnvironmentStatus.Failed)]
    [InlineData("Succeeded", "Terminated", EnvironmentStatus.Failed)]
    [InlineData("succeeded", "running", EnvironmentStatus.Running)]
    public void Maps_aci_states_to_environment_status(string? provisioning, string? instance, EnvironmentStatus expected)
    {
        Assert.Equal(expected, AciStatus.Map(provisioning, instance));
    }

    [Theory]
    [InlineData("Waiting", EnvironmentStatus.Provisioning)] // after Start the group says Running while the image is still pulling
    [InlineData("waiting", EnvironmentStatus.Provisioning)]
    [InlineData("Running", EnvironmentStatus.Running)]
    [InlineData(null, EnvironmentStatus.Running)]
    public void A_running_group_is_only_running_once_its_container_is(string? containerState, EnvironmentStatus expected)
    {
        Assert.Equal(expected, AciStatus.Map("Succeeded", "Running", containerState));
    }

    [Fact]
    public void A_waiting_container_does_not_mask_a_stopped_or_failed_group()
    {
        Assert.Equal(EnvironmentStatus.Stopped, AciStatus.Map("Succeeded", "Stopped", "Waiting"));
        Assert.Equal(EnvironmentStatus.Failed, AciStatus.Map("Succeeded", "Failed", "Waiting"));
    }
}
