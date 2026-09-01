namespace Engine.Kernel.World;

/// <summary>
/// The entity type: identity, hierarchy, and a list of components. See
/// docs/kernel-contract.md §2.
/// </summary>
public sealed class GameObject
{
    public required string Name { get; set; }

    public Transform Transform;

    public GameObject? Parent { get; internal set; }

    // TODO(M0): backing storage for children, tags, and the component list
    // + type index that makes World.Query<T>() O(matches), not O(all).

    public IReadOnlyList<GameObject> Children => throw new NotImplementedException();

    public T? GetComponent<T>() where T : Component
        => throw new NotImplementedException();

    public T AddComponent<T>() where T : Component, new()
        => throw new NotImplementedException();

    public void RemoveComponent<T>() where T : Component
        => throw new NotImplementedException();
}
