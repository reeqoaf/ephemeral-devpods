namespace EphemeralDevpods.Core;

/// <summary>Base for failures caused by client input; the message is safe to return to the caller as a 400.</summary>
public abstract class UserInputException(string message) : Exception(message);
