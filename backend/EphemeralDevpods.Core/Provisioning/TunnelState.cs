namespace EphemeralDevpods.Core.Provisioning;

public enum TunnelPhase
{
    Starting,
    AwaitingLogin,
    Ready,
}

/// <summary>
/// Live state of an environment's VS Code tunnel. Never persisted — a device code is short-lived and
/// only meaningful while <see cref="TunnelPhase.AwaitingLogin"/>.
/// </summary>
public sealed record TunnelState(TunnelPhase Phase, string? DeviceCode = null, string? VerificationUrl = null);
