using Engine.Kernel.World;

namespace Sandbox.Echo.Contracts;

/// <summary>
/// Minimal component for exercising the reload loop described in
/// docs/kernel-contract.md §7 — not a real engine feature, just something
/// for a system to touch before any real plugin exists.
/// </summary>
public sealed class Ping : Component
{
    public int Count;
}
