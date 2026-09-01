using Engine.Kernel.World;

namespace Engine.Kernel.Scheduling;

/// <summary>
/// Frame stages and system ordering. See docs/kernel-contract.md §2.
/// </summary>
public interface ISchedule
{
    /// <summary>
    /// <c>Action&lt;IWorld&gt;</c> specifically, not <c>Delegate</c> — a
    /// lambda passed where the target type is exactly <c>Delegate</c>
    /// doesn't reliably compile down to <c>System.Action&lt;IWorld&gt;</c>
    /// at runtime (the compiler's natural-type inference for lambdas can
    /// synthesize a different, unspeakable delegate type instead, so a
    /// runtime <c>is Action&lt;IWorld&gt;</c> check silently fails). Method
    /// groups and lambdas both convert to <c>Action&lt;IWorld&gt;</c>
    /// correctly when it's the actual parameter type — see §3 for both
    /// forms in use.
    /// </summary>
    ISystemBuilder Add(Stage stage, Action<IWorld> system);

    /// <summary>Called from a plugin's Shutdown() — must remove everything
    /// Configure() added, or the ALC it lives in will never unload. See §4.</summary>
    void RemoveAllFrom(string pluginId);
}
