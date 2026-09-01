using Engine.Kernel.World;

namespace Engine.Kernel.Scheduling;

/// <summary>
/// Fluent builder returned by <see cref="ISchedule.Add"/>. Declared
/// Reads/Writes are what lets the scheduler run systems with disjoint
/// access in parallel and, in debug builds, enforce that a system only
/// touches what it declared. See docs/kernel-contract.md §2 and §7.
/// </summary>
public interface ISystemBuilder
{
    ISystemBuilder After(string systemId);
    ISystemBuilder Reads<T>() where T : Component;
    ISystemBuilder Writes<T>() where T : Component;
}
