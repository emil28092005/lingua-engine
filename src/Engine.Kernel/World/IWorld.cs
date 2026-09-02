namespace Engine.Kernel.World;

/// <summary>
/// Kernel-owned storage for every <see cref="GameObject"/> in a scene.
/// Shape sketched from its usage throughout docs/kernel-contract.md — not a
/// final API; M0's job is to actually implement this.
/// </summary>
public interface IWorld
{
    /// <summary>Top-level GameObjects — everything with no parent.</summary>
    IReadOnlyList<GameObject> Roots { get; }

    GameObject CreateGameObject(string name);

    void Destroy(GameObject go);

    /// <summary>Type-indexed lookup — O(matches), not O(all). See §2.</summary>
    IEnumerable<GameObject> Query<T>() where T : Component;

    /// <summary>
    /// A deep, opaque snapshot of every GameObject and Component — for
    /// Play mode (§5): taken on EnterPlay, handed back to
    /// <see cref="Restore"/> on ExitPlay to discard whatever changed while
    /// playing. Backed by <see cref="SceneFormat"/> rather than a
    /// separate clone mechanism — a scene file and a Play-mode snapshot
    /// are the same problem (capture every GameObject's state, faithfully
    /// enough to reconstruct it) at two different moments, and reusing
    /// already-proven serialization is cheaper than maintaining a second
    /// way to walk the same graph.
    /// </summary>
    string Snapshot();

    /// <summary>Destroys every current root and rebuilds the graph
    /// <paramref name="snapshot"/> describes. Not merged with what's
    /// there — Play mode always restores onto a world it's about to fully
    /// own, so additive Load() semantics would be the wrong default here,
    /// unlike SceneFormat.Load's own.</summary>
    void Restore(string snapshot);
}
