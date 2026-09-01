using System.Reflection;

namespace Engine.Kernel.Scheduling;

/// <summary>
/// Bookkeeping only for now — no stage execution, no parallelism, no
/// enforcement of declared access. That's the real Scheduler work described
/// in docs/kernel-contract.md §2 and §7; this pass exists to make
/// <see cref="RemoveAllFrom"/> actually correct, because it's the one thing
/// PluginHost's reload correctness depends on.
///
/// Ownership is tracked by the registering delegate's declaring assembly,
/// not by a string tag on each entry — a plugin can only accidentally
/// mislabel a system if it hands another plugin's delegate to Add(), which
/// isn't a realistic failure mode. See <see cref="RegisterPlugin"/>.
/// </summary>
public sealed class Schedule : ISchedule
{
    private readonly List<SystemEntry> _systems = [];
    private readonly Dictionary<string, Assembly> _pluginAssemblies = [];

    /// <summary>Called by PluginHost right after loading a plugin's
    /// implementation assembly, before Configure() runs.</summary>
    internal void RegisterPlugin(string pluginId, Assembly implementationAssembly)
        => _pluginAssemblies[pluginId] = implementationAssembly;

    public ISystemBuilder Add(Stage stage, Delegate system)
    {
        var entry = new SystemEntry(stage, system);
        _systems.Add(entry);
        return new SystemBuilder(entry);
    }

    public void RemoveAllFrom(string pluginId)
    {
        // Removing the assembly mapping here too, not just the systems —
        // leaving it behind would itself be a stray reference into the
        // ALC the caller is about to unload.
        if (_pluginAssemblies.Remove(pluginId, out var assembly))
            _systems.RemoveAll(e => e.System.Method.DeclaringType?.Assembly == assembly);
    }
}
