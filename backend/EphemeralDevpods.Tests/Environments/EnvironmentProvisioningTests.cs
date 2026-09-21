using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Provisioning;
using EphemeralDevpods.Functions.Functions.Environments;
using Microsoft.Extensions.Logging.Abstractions;

namespace EphemeralDevpods.Tests.Environments;

public class EnvironmentProvisioningTests
{
    private readonly FakeProvisioner _provisioner = new();
    private readonly FakeRepository _environments = new();
    private readonly FakeResourceRepository _resources = new();

    private EnvironmentProvisioning Create() =>
        new(_provisioner, _environments, _resources, NullLogger<EnvironmentProvisioning>.Instance);

    private WorkspaceEnvironment Queued(EnvironmentStatus status = EnvironmentStatus.Provisioning)
    {
        var environment = TestEnvironments.Make(status);
        _environments.Rows[environment.EnvironmentId] = environment;
        return environment;
    }

    private static ProvisionRequest RequestFor(WorkspaceEnvironment environment) =>
        new(environment.Owner, environment.EnvironmentId, new EnvironmentSpec { Image = "node:22" });

    [Fact]
    public async Task Marks_the_environment_running_and_stores_the_result()
    {
        var environment = Queued();
        _provisioner.ProvisionOutcome = new ProvisionResult
        {
            PublicUrl = "",
            AccessToken = "",
            TunnelName = "epd-abcdef12",
            Resources =
            [
                new DeployedResource
                {
                    EnvironmentId = environment.EnvironmentId,
                    ResourceId = "group",
                    Type = ResourceType.ContainerGroup,
                    ProviderResourceId = "/subscriptions/x",
                    Status = "Running",
                },
            ],
        };

        await Create().RunAsync(RequestFor(environment), CancellationToken.None);

        var stored = _environments.Rows[environment.EnvironmentId];
        Assert.Equal(EnvironmentStatus.Running, stored.Status);
        Assert.Equal("epd-abcdef12", stored.TunnelName);
        Assert.Single(_resources.Stored);
    }

    [Fact]
    public async Task A_provisioning_failure_marks_the_environment_failed_instead_of_throwing()
    {
        var environment = Queued();
        _provisioner.ProvisionFailure = new InvalidOperationException("image pull failed");

        await Create().RunAsync(RequestFor(environment), CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Failed, _environments.Rows[environment.EnvironmentId].Status);
        Assert.Empty(_resources.Stored);
    }

    [Fact]
    public async Task A_host_shutdown_is_not_recorded_as_a_failure()
    {
        var environment = Queued();
        using var cts = new CancellationTokenSource();
        _provisioner.DuringProvision = () =>
        {
            cts.Cancel();
            throw new OperationCanceledException(cts.Token);
        };

        await Assert.ThrowsAsync<OperationCanceledException>(() => Create().RunAsync(RequestFor(environment), cts.Token));

        Assert.Equal(EnvironmentStatus.Provisioning, _environments.Rows[environment.EnvironmentId].Status);
    }

    [Theory]
    [InlineData(EnvironmentStatus.Running)]
    [InlineData(EnvironmentStatus.Expired)]
    [InlineData(EnvironmentStatus.Failed)]
    public async Task Ignores_a_message_for_an_environment_that_is_no_longer_provisioning(EnvironmentStatus status)
    {
        var environment = Queued(status);
        _provisioner.ProvisionFailure = new InvalidOperationException("must not be called");

        await Create().RunAsync(RequestFor(environment), CancellationToken.None);

        Assert.Equal(status, _environments.Rows[environment.EnvironmentId].Status);
    }

    [Fact]
    public async Task Ignores_a_message_for_an_unknown_environment()
    {
        _provisioner.ProvisionFailure = new InvalidOperationException("must not be called");

        await Create().RunAsync(
            new ProvisionRequest("owner", Guid.NewGuid().ToString(), new EnvironmentSpec { Image = "node:22" }),
            CancellationToken.None);
    }

    [Fact]
    public async Task Tears_down_what_it_built_when_the_environment_was_deleted_meanwhile()
    {
        var environment = Queued();
        _provisioner.DuringProvision = () =>
        {
            _environments.Rows[environment.EnvironmentId].Status = EnvironmentStatus.Expired; // the user hit Delete
            return Task.CompletedTask;
        };

        await Create().RunAsync(RequestFor(environment), CancellationToken.None);

        Assert.Equal(EnvironmentStatus.Expired, _environments.Rows[environment.EnvironmentId].Status);
        Assert.Equal([environment.EnvironmentId], _provisioner.TornDown);
        Assert.Empty(_resources.Stored);
    }

    [Fact]
    public void A_provision_request_survives_the_queue_round_trip()
    {
        var spec = new EnvironmentSpec
        {
            Name = "demo",
            Image = "node:22",
            DevcontainerBaseDirectory = ".devcontainer",
            BuildArgs = new Dictionary<string, string> { ["A"] = "1" },
            ForwardPorts = [3000, 5173],
            ContainerEnv = new Dictionary<string, string> { ["KEY"] = "value" },
            PostCreateCommand = ["npm install"],
            PostAttachCommand = ["npm", "start"],
        };

        var roundTripped = ProvisionRequest.FromMessage(new ProvisionRequest("owner", "env-1", spec).ToMessage());

        Assert.Equal("owner", roundTripped.Owner);
        Assert.Equal("env-1", roundTripped.EnvironmentId);
        Assert.Equal("demo", roundTripped.Spec.Name);
        Assert.Equal([3000, 5173], roundTripped.Spec.ForwardPorts);
        Assert.Equal("1", roundTripped.Spec.BuildArgs["A"]);
        Assert.Equal("value", roundTripped.Spec.ContainerEnv["KEY"]);
        Assert.Equal(["npm", "start"], roundTripped.Spec.PostAttachCommand);
        Assert.Equal(".devcontainer", roundTripped.Spec.DevcontainerBaseDirectory);
    }
}
