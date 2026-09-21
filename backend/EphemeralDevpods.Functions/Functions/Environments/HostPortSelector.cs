using EphemeralDevpods.Core.Models;
using EphemeralDevpods.Core.Repositories;

namespace EphemeralDevpods.Functions.Functions.Environments;

/// <summary>
/// Validates the host port each forwarded container port is published at (local Docker). The user picks them,
/// defaulting to the container port's own number. Nothing checks the machine; the only thing refused is a port
/// another environment still holds — a stopped environment isn't listening, but needs its port back on Start.
/// </summary>
public sealed class HostPortSelector(IEnvironmentRepository environments)
{
    private const int MinHostPort = 1;
    private const int MaxHostPort = 65535;

    /// <summary>The mappings a new environment will use: what the caller chose, or each port at its own number when it sent none.</summary>
    /// <exception cref="InvalidEnvironmentOptionException">The mappings don't match the repo's forwarded ports, or a port is out of range.</exception>
    /// <exception cref="HostPortInUseException">Another environment already holds a chosen port.</exception>
    public async Task<IReadOnlyList<PortMapping>> ResolveAsync(
        IReadOnlyList<int> containerPorts, IReadOnlyList<PortMapping>? requested, CancellationToken ct)
    {
        requested ??= PortMapping.Identity(containerPorts);

        var expected = containerPorts.Distinct().ToList();
        if (requested.Count != expected.Count || !expected.All(port => requested.Any(m => m.ContainerPort == port)))
        {
            throw new InvalidEnvironmentOptionException("Port mappings don't match the repository's forwarded ports.");
        }

        var hostPorts = requested.Select(m => m.HostPort).ToList();
        if (hostPorts.Any(port => port is < MinHostPort or > MaxHostPort))
        {
            throw new InvalidEnvironmentOptionException($"Host ports must be between {MinHostPort} and {MaxHostPort}.");
        }

        if (hostPorts.Distinct().Count() != hostPorts.Count)
        {
            throw new InvalidEnvironmentOptionException("Each forwarded port needs its own host port.");
        }

        var held = (await environments.ListActiveAsync(ct))
            .SelectMany(environment => environment.PortMappings ?? [])
            .Select(mapping => mapping.HostPort)
            .ToHashSet();

        var clash = hostPorts.FirstOrDefault(held.Contains);
        if (clash != 0)
        {
            throw new HostPortInUseException(clash);
        }

        return expected.Select(port => requested.Single(m => m.ContainerPort == port)).ToList();
    }
}
