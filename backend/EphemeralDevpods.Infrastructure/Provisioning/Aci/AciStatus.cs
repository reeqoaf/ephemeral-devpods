using EphemeralDevpods.Core.Models;

namespace EphemeralDevpods.Infrastructure.Provisioning.Aci;

/// <summary>Maps an ACI container group's reported states onto <see cref="EnvironmentStatus"/>.</summary>
public static class AciStatus
{
    /// <param name="provisioningState">ARM provisioning state of the group (Creating, Succeeded, Failed, ...).</param>
    /// <param name="instanceState">Instance-view state of the running group (Pending, Running, Stopped, Succeeded, Failed, ...); null until it exists.</param>
    public static EnvironmentStatus Map(string? provisioningState, string? instanceState)
    {
        if (string.IsNullOrEmpty(instanceState))
        {
            // No instance view yet: still being created, unless ARM already gave up on the deployment.
            return Is(provisioningState, "Failed") ? EnvironmentStatus.Failed : EnvironmentStatus.Provisioning;
        }

        if (Is(instanceState, "Running"))
        {
            return EnvironmentStatus.Running;
        }

        if (Is(instanceState, "Pending"))
        {
            return EnvironmentStatus.Provisioning;
        }

        if (Is(instanceState, "Stopped"))
        {
            return EnvironmentStatus.Stopped;
        }

        // Succeeded / Failed / Terminated: with restart policy Never the entrypoint exited, which for a dev
        // environment means it died (same reading as an exited local container).
        return EnvironmentStatus.Failed;
    }

    private static bool Is(string? value, string expected) =>
        string.Equals(value, expected, StringComparison.OrdinalIgnoreCase);
}
