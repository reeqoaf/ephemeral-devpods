namespace EphemeralDevpods.Core.Parsing;

/// <summary>Thrown when a repo has no devcontainer.json at either supported location.</summary>
public sealed class DevcontainerNotFoundException()
    : UserInputException("No devcontainer.json found (checked .devcontainer/devcontainer.json and .devcontainer.json).");
