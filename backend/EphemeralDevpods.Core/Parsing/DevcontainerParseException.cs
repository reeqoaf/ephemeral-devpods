namespace EphemeralDevpods.Core.Parsing;

/// <summary>Thrown when devcontainer.json uses something v1 doesn't support, or isn't valid JSONC.</summary>
public sealed class DevcontainerParseException(string message) : Exception(message);
