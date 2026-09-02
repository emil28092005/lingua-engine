namespace Engine.Kernel.Scheduling;

/// <summary>
/// Ambient, per-thread record of which component types the currently
/// running system declared via Reads&lt;T&gt;()/Writes&lt;T&gt;(). GameWorld
/// and GameObject consult this — when one is active — to enforce
/// docs/kernel-contract.md §7's rule that an undeclared access fails
/// loudly instead of silently working by accident.
///
/// No scope is active outside of Schedule.RunStage's invocation of a
/// system — editor code, tests, and initial scene construction are all
/// unconstrained by design; enforcement exists for the frame loop, not for
/// every touch of a GameObject anywhere in the process.
///
/// ThreadLocal rather than a plain static field: batches run sequentially
/// today (see the TODO on Schedule.RunStage), but this is already correct
/// for when a batch's systems run on separate threads instead.
/// </summary>
internal static class SystemAccessScope
{
    private static readonly ThreadLocal<(IReadOnlySet<Type> Reads, IReadOnlySet<Type> Writes)?> Current = new();

    public static IDisposable Enter(IReadOnlySet<Type> reads, IReadOnlySet<Type> writes)
    {
        var previous = Current.Value;
        Current.Value = (reads, writes);
        return new Restore(previous);
    }

    /// <summary>
    /// Clears any active scope for the duration of a bulk, whole-world
    /// operation — GameWorld.Restore is the one real caller. Restore
    /// rebuilds arbitrary component types from a snapshot via
    /// GameObject.AddComponent, and it can be invoked from inside a
    /// running system (a Play/Stop button click handled during Stage.
    /// Render, say) as easily as from unconstrained editor/host code. This
    /// class's own doc comment already says editor code and scene
    /// construction are unconstrained by design; without this, that's only
    /// true when the call happens to originate outside any system's own
    /// scope, not because Restore is actually exempt — a real bug, not a
    /// hypothetical one: entering then exiting Play mode from the editor's
    /// own Stop button throws, because ExitPlay runs inside EditorPlugin's
    /// Stage.Render system, which (correctly) never declares Writes&lt;T&gt;()
    /// for component types it has no compile-time knowledge of.
    /// </summary>
    public static IDisposable Suspend()
    {
        var previous = Current.Value;
        Current.Value = null;
        return new Restore(previous);
    }

    /// <summary>Querying or fetching a component counts as a read — either
    /// Reads&lt;T&gt;() or Writes&lt;T&gt;() satisfies it.</summary>
    public static void CheckRead(Type componentType)
    {
        var scope = Current.Value;
        if (scope is null)
            return;

        if (!scope.Value.Reads.Contains(componentType) && !scope.Value.Writes.Contains(componentType))
        {
            throw new InvalidOperationException(
                $"A system read '{componentType.Name}' without declaring Reads<{componentType.Name}>() " +
                $"or Writes<{componentType.Name}>() — see docs/kernel-contract.md §7.");
        }
    }

    /// <summary>Structurally changing a GameObject's components — adding or
    /// removing one — requires Writes&lt;T&gt;() specifically. Mutating a
    /// component's own fields after GetComponent&lt;T&gt;() isn't
    /// interceptable this way; see the note on GameObject.AddComponent.</summary>
    public static void CheckWrite(Type componentType)
    {
        var scope = Current.Value;
        if (scope is null)
            return;

        if (!scope.Value.Writes.Contains(componentType))
        {
            throw new InvalidOperationException(
                $"A system structurally changed '{componentType.Name}' without declaring " +
                $"Writes<{componentType.Name}>() — see docs/kernel-contract.md §7.");
        }
    }

    private sealed class Restore((IReadOnlySet<Type> Reads, IReadOnlySet<Type> Writes)? previous) : IDisposable
    {
        public void Dispose() => Current.Value = previous;
    }
}
