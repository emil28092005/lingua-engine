using Engine.Kernel.Scheduling;

namespace Engine.Kernel.World;

/// <summary>
/// Kernel-owned storage for every GameObject in a scene. See
/// docs/kernel-contract.md §2.
///
/// Named <c>GameWorld</c> rather than <c>World</c> — a class with the same
/// simple name as its own containing namespace (<c>Engine.Kernel.World</c>)
/// makes the bare name ambiguous for every consumer, since C# resolves the
/// enclosing namespace before an imported type.
/// </summary>
public sealed class GameWorld : IWorld
{
    private readonly List<GameObject> _roots = [];
    private readonly Dictionary<Type, HashSet<GameObject>> _index = [];

    /// <summary>Top-level GameObjects — everything with no parent. Walking
    /// a scene starts here.</summary>
    public IReadOnlyList<GameObject> Roots => _roots;

    public GameObject CreateGameObject(string name)
    {
        var go = new GameObject(name)
        {
            Owner = this,
            Transform = Transform.Identity,
        };

        _roots.Add(go);
        return go;
    }

    public void Destroy(GameObject go)
    {
        // Children first: a GameObject can't outlive the world that
        // indexes it. Snapshot to an array — Destroy(child) mutates
        // go.Children out from under a live enumeration otherwise.
        foreach (var child in go.Children.ToArray())
            Destroy(child);

        RemoveFromAllIndices(go);

        if (go.Parent is null)
            _roots.Remove(go);
        else
            go.DetachFromParent();

        go.Owner = null;
    }

    /// <summary>Type-indexed lookup — O(matches), not O(all). See §2.
    ///
    /// Iterating this while structurally mutating the world (adding or
    /// removing a GameObject or component) throws, by design: correctness
    /// over silently returning a stale or partial result. A Scheduler is
    /// expected to queue structural changes to a stage boundary rather than
    /// let a running system trigger this — see the Scheduler row in §2.
    /// </summary>
    public IEnumerable<GameObject> Query<T>() where T : Component
    {
        SystemAccessScope.CheckRead(typeof(T));
        return _index.TryGetValue(typeof(T), out var set) ? set : [];
    }

    internal void IndexComponentAdded(GameObject go, Component component)
    {
        var type = component.GetType();

        if (!_index.TryGetValue(type, out var set))
        {
            set = [];
            _index[type] = set;
        }

        set.Add(go);
    }

    internal void IndexComponentRemoved(GameObject go, Component removed)
    {
        var type = removed.GetType();

        if (!_index.TryGetValue(type, out var set))
            return;

        // A GameObject can carry more than one component of the same type
        // (AddComponent<T>() doesn't enforce uniqueness). Only drop it from
        // the index once none are left — checked against go.Components,
        // which by this point no longer includes the one just removed.
        var stillHasOne = false;
        foreach (var c in go.Components)
        {
            if (c.GetType() != type)
                continue;
            stillHasOne = true;
            break;
        }

        if (!stillHasOne)
            set.Remove(go);
    }

    internal void OnReparented(GameObject go, GameObject? oldParent, GameObject? newParent)
    {
        if (oldParent is null && newParent is not null)
            _roots.Remove(go);
        else if (oldParent is not null && newParent is null)
            _roots.Add(go);
    }

    public string Snapshot() => SceneFormat.ToJson(this);

    public void Restore(string snapshot)
    {
        foreach (var root in Roots.ToArray())
            Destroy(root);

        SceneFormat.FromJson(this, snapshot);
    }

    /// <summary>Unconditional removal from every type bucket, used by
    /// Destroy — cheaper to reason about than replaying per-component
    /// removals through IndexComponentRemoved's "still has one left?"
    /// check, which assumes the component list it inspects still reflects
    /// reality. O(distinct component types ever indexed); revisit only if
    /// that count grows large enough to matter.</summary>
    private void RemoveFromAllIndices(GameObject go)
    {
        foreach (var set in _index.Values)
            set.Remove(go);
    }
}
