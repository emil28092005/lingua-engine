namespace Engine.Kernel.Scheduling;

using Engine.Kernel.World;

/// <summary>
/// One registered system. Reads/Writes are recorded here so a future
/// Scheduler can build the conflict graph described in docs/kernel-contract.md
/// §2 — nothing consumes them yet; that's real Scheduler work, not this pass.
/// </summary>
internal sealed class SystemEntry(Stage stage, Delegate system)
{
    public Stage Stage { get; } = stage;
    public Delegate System { get; } = system;
    public string? After { get; set; }
    public HashSet<Type> Reads { get; } = [];
    public HashSet<Type> Writes { get; } = [];
}
