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

    // TODO(§5): Snapshot()/Restore() for Play mode — a deep clone of the
    // GameObject graph, taken on EnterPlay and discarded on ExitPlay.
}
