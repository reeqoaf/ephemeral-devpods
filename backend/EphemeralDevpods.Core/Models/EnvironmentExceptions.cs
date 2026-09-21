using EphemeralDevpods.Core.Auth;

namespace EphemeralDevpods.Core.Models;

/// <summary>A create request asked for a name/TTL/resources/provider outside what's currently offered. Maps to 400.</summary>
public sealed class InvalidEnvironmentOptionException(string message) : UserInputException(message);

/// <summary>A lifecycle action (stop/start/restart) isn't valid for the environment's current status. Maps to 409.</summary>
public sealed class InvalidEnvironmentStateException(string action, EnvironmentStatus status)
    : ConflictException($"Can't {action} an environment that is {status}.");

/// <summary>The chosen host port is already held by another environment (a stopped one still keeps its port). Maps to 409.</summary>
public sealed class HostPortInUseException(int port)
    : ConflictException($"Port {port} is already used by another environment. Pick a different port.");
