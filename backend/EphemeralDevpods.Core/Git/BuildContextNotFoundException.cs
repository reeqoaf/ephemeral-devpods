namespace EphemeralDevpods.Core.Git;

/// <summary>Thrown when a devcontainer's build.context path matches no files in the fetched repo.</summary>
public sealed class BuildContextNotFoundException(string message) : UserInputException(message);
