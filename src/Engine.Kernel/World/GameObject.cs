using System.Numerics;

namespace Engine.Kernel.World;

/// <summary>
/// The entity type: identity, hierarchy, and a list of components. See
/// docs/kernel-contract.md §2.
///
/// Construction is internal — the only way to get one is
/// <see cref="IWorld.CreateGameObject"/>, so <see cref="World"/> can keep
/// its type index consistent instead of trusting callers to report changes.
/// </summary>
public sealed class GameObject
{
    private readonly List<Component> _components = [];
    private readonly List<GameObject> _children = [];

    internal GameObject(string name)
    {
        Name = name;
    }

    public string Name { get; set; }

    public Transform Transform;

    public GameObject? Parent { get; private set; }

    public IReadOnlyList<GameObject> Children => _children;

    public IReadOnlyList<Component> Components => _components;

    /// <summary>
    /// Set by <see cref="GameWorld"/> at creation and cleared on destroy.
    /// Null means "not attached to a World" — a defensive state that
    /// should never be observable from a plugin.
    /// </summary>
    internal GameWorld? Owner { get; set; }

    /// <summary>
    /// Composed from the parent chain on every read, not cached — a cache
    /// here would need invalidating on every reparent and every ancestor's
    /// transform change, which is more bookkeeping than recomputing a
    /// handful of matrix multiplies costs at indie scale.
    /// </summary>
    public Matrix4x4 WorldMatrix =>
        Parent is null ? Transform.LocalMatrix : Transform.LocalMatrix * Parent.WorldMatrix;

    /// <summary>
    /// Reparents this GameObject. Throws if that would create a cycle —
    /// checked by walking <paramref name="parent"/>'s own ancestors for
    /// this object, which is cheap next to the cost of silently corrupting
    /// the hierarchy.
    /// </summary>
    public void SetParent(GameObject? parent)
    {
        if (ReferenceEquals(parent, this))
            throw new InvalidOperationException($"GameObject '{Name}' cannot be its own parent.");

        for (var ancestor = parent?.Parent; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, this))
                throw new InvalidOperationException(
                    $"Setting '{parent!.Name}' as the parent of '{Name}' would create a cycle.");
        }

        if (ReferenceEquals(Parent, parent))
            return;

        var oldParent = Parent;
        oldParent?._children.Remove(this);
        Parent = parent;
        parent?._children.Add(this);

        Owner?.OnReparented(this, oldParent, parent);
    }

    public T? GetComponent<T>() where T : Component
    {
        foreach (var component in _components)
        {
            if (component is T match)
                return match;
        }

        return null;
    }

    public T AddComponent<T>() where T : Component, new()
    {
        var component = new T();
        _components.Add(component);
        Owner?.IndexComponentAdded(this, component);
        return component;
    }

    public void RemoveComponent<T>() where T : Component
    {
        for (var i = 0; i < _components.Count; i++)
        {
            if (_components[i] is not T match)
                continue;

            _components.RemoveAt(i);
            Owner?.IndexComponentRemoved(this, match);
            return;
        }
    }

    /// <summary>Used only by GameWorld.Destroy, which handles index and
    /// roots bookkeeping itself — see the note there on why this bypasses
    /// SetParent's cycle check and reparent notification.</summary>
    internal void DetachFromParent()
    {
        Parent?._children.Remove(this);
        Parent = null;
    }
}
