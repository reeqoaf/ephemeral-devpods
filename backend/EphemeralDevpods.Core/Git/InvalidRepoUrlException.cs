namespace EphemeralDevpods.Core.Git;

/// <summary>Thrown when a repo URL isn't a supported/parsable github.com URL.</summary>
public sealed class InvalidRepoUrlException(string message) : UserInputException(message);
