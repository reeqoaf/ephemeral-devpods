namespace EphemeralDevpods.Core.Models;

/// <summary>A port the environment's app listens on inside the container, and the port on the host it is published at.</summary>
public sealed record PortMapping(int ContainerPort, int HostPort)
{
    /// <summary>Each distinct container port published at the same number on the host, in order (the first is the main app port).</summary>
    public static IReadOnlyList<PortMapping> Identity(IEnumerable<int> containerPorts) =>
        containerPorts.Distinct().Select(port => new PortMapping(port, port)).ToList();

    /// <summary>"3000:24817,5173:24818" — the compact form stored in the environments table.</summary>
    public static string Format(IEnumerable<PortMapping> mappings) =>
        string.Join(',', mappings.Select(m => $"{m.ContainerPort}:{m.HostPort}"));

    public static IReadOnlyList<PortMapping> ParseList(string? stored) =>
        string.IsNullOrWhiteSpace(stored)
            ? []
            : stored.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(pair => pair.Split(':'))
                .Select(parts => new PortMapping(int.Parse(parts[0]), int.Parse(parts[1])))
                .ToList();
}
