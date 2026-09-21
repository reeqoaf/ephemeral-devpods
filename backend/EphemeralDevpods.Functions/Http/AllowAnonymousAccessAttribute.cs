namespace EphemeralDevpods.Functions.Http;

/// <summary>
/// Opts a function out of <see cref="AuthMiddleware"/>'s default-deny. Every HTTP function requires a valid
/// session unless it carries this attribute — so a new endpoint is protected by default (§6).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AllowAnonymousAccessAttribute : Attribute;
