namespace Engine.Kernel.World;

/// <summary>
/// Base class for all component data. Plain fields, no methods, no
/// lifecycle hooks — behavior lives in systems, never on the component
/// itself. See docs/kernel-contract.md §1 and §7.
/// </summary>
public abstract class Component
{
    // Intentionally empty. A subclass adds fields only.
}
