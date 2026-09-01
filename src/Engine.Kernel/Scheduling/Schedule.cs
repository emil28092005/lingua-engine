using System.Reflection;
using Engine.Kernel.World;

namespace Engine.Kernel.Scheduling;

/// <summary>
/// Registration plus execution. See docs/kernel-contract.md §2 and §7.
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

    public ISystemBuilder Add(Stage stage, Action<IWorld> system)
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

    /// <summary>
    /// Runs every system registered for <paramref name="stage"/>, grouped
    /// into conflict-free batches by declared Reads/Writes (see
    /// <see cref="ComputeBatches"/>) and, in DEBUG builds, enforced against
    /// what each one actually touches (see SystemAccessScope).
    ///
    /// TODO: batches are computed but run sequentially, not on separate
    /// threads — real parallel dispatch needs structural changes
    /// (Create/Destroy/AddComponent/RemoveComponent) deferred to a command
    /// buffer flushed after the batch first. Without that, two systems with
    /// disjoint *declared* types can still race on shared, non-thread-safe
    /// storage: AddComponent&lt;T&gt;() on a GameObject mutates that
    /// GameObject's own component list regardless of T, and GameWorld's
    /// index/roots collections aren't safe for concurrent mutation either.
    /// See the Scheduler row in docs/kernel-contract.md §2.
    /// </summary>
    public void RunStage(Stage stage, IWorld world)
    {
        var stageSystems = _systems.Where(e => e.Stage == stage).ToList();

        foreach (var batch in ComputeBatches(stageSystems))
        {
            foreach (var entry in batch)
                Invoke(entry, world);
        }
    }

    private static void Invoke(SystemEntry entry, IWorld world)
    {
        using var _ = SystemAccessScope.Enter(entry.Reads, entry.Writes);
        entry.System(world);
    }

    /// <summary>
    /// Groups systems into the fewest sequential batches such that no two
    /// systems in the same batch conflict — greedy first-fit, preserving
    /// registration order. Not yet consumed for real parallelism (see the
    /// TODO on RunStage), but exercised directly by ScheduleTests so the
    /// conflict logic itself is validated independent of that.
    /// </summary>
    internal static List<List<SystemEntry>> ComputeBatches(IReadOnlyList<SystemEntry> systems)
    {
        var batches = new List<List<SystemEntry>>();

        foreach (var entry in systems)
        {
            var batch = batches.FirstOrDefault(b => b.TrueForAll(other => !Conflicts(entry, other)));

            if (batch is not null)
                batch.Add(entry);
            else
                batches.Add([entry]);
        }

        return batches;
    }

    private static bool Conflicts(SystemEntry a, SystemEntry b) =>
        a.Writes.Overlaps(b.Reads) || a.Writes.Overlaps(b.Writes) || b.Writes.Overlaps(a.Reads);
}
