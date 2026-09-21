namespace EphemeralDevpods.Core.Models;

/// <summary>
/// The user-selectable settings of a new environment, validated against the values currently on offer.
/// The frontend mirrors these sets in its option dropdowns; widening a set here is all the backend
/// needs to accept (and enforce) a new choice.
/// </summary>
public sealed record EnvironmentOptions(
    string Name, int TtlMinutes, int CpuCores, int MemoryMb, TunnelProvider TunnelProvider)
{
    public const int MaxNameLength = 64;

    public static readonly IReadOnlyList<int> AllowedTtlMinutes = [60];
    public static readonly IReadOnlyList<int> AllowedCpuCores = [2];
    public static readonly IReadOnlyList<int> AllowedMemoryMb = [4096];
    public static readonly IReadOnlyList<TunnelProvider> AllowedTunnelProviders = [TunnelProvider.GitHub];

    /// <param name="fallbackName">Used when the caller gave no name (e.g. the devcontainer's name or the repo name).</param>
    /// <exception cref="InvalidEnvironmentOptionException">A value is outside what's currently offered.</exception>
    public static EnvironmentOptions Resolve(
        string? name, int? ttlMinutes, int? cpuCores, int? memoryMb, string? tunnelProvider, string fallbackName)
    {
        var trimmedName = string.IsNullOrWhiteSpace(name) ? fallbackName.Trim() : name.Trim();
        if (trimmedName.Length is 0 or > MaxNameLength)
        {
            throw new InvalidEnvironmentOptionException($"Name must be 1-{MaxNameLength} characters.");
        }

        return new EnvironmentOptions(
            trimmedName,
            Pick(ttlMinutes, AllowedTtlMinutes, "ttlMinutes"),
            Pick(cpuCores, AllowedCpuCores, "cpuCores"),
            Pick(memoryMb, AllowedMemoryMb, "memoryMb"),
            ParseProvider(tunnelProvider));
    }

    private static int Pick(int? requested, IReadOnlyList<int> allowed, string field)
    {
        if (requested is null)
        {
            return allowed[0];
        }

        return allowed.Contains(requested.Value)
            ? requested.Value
            : throw new InvalidEnvironmentOptionException($"{field} must be one of: {string.Join(", ", allowed)}.");
    }

    private static TunnelProvider ParseProvider(string? requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return AllowedTunnelProviders[0];
        }

        if (!Enum.TryParse<TunnelProvider>(requested, ignoreCase: true, out var provider) ||
            !Enum.IsDefined(provider))
        {
            throw new InvalidEnvironmentOptionException($"Unknown tunnel provider: {requested}.");
        }

        return AllowedTunnelProviders.Contains(provider)
            ? provider
            : throw new InvalidEnvironmentOptionException($"Tunnel login with {provider} isn't supported yet.");
    }
}
