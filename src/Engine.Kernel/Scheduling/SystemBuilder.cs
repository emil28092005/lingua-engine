namespace Engine.Kernel.Scheduling;

using Engine.Kernel.World;

internal sealed class SystemBuilder(SystemEntry entry) : ISystemBuilder
{
    public ISystemBuilder After(string systemId)
    {
        entry.After = systemId;
        return this;
    }

    public ISystemBuilder Reads<T>() where T : Component
    {
        entry.Reads.Add(typeof(T));
        return this;
    }

    public ISystemBuilder Writes<T>() where T : Component
    {
        entry.Writes.Add(typeof(T));
        return this;
    }
}
