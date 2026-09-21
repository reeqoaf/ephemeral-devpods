namespace EphemeralDevpods.Infrastructure.Provisioning;

/// <summary>Names derived from an environment id that every compute backend must agree on.</summary>
public static class EnvironmentNaming
{
    /// <summary>Short and stable so it fits in the vscode.dev URL; the 8-char prefix of the GUID is unique enough per account.</summary>
    public static string TunnelNameFor(string environmentId) => $"epd-{environmentId[..8]}";
}
