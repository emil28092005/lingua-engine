using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

namespace Engine.Kernel.Plugins;

/// <summary>One per loaded plugin, built by PluginHost. Everything it
/// exposes except Log is a shared, kernel-owned singleton — the context
/// itself is the only thing scoped per plugin.</summary>
internal sealed class PluginContext(
    string pluginId,
    IWorld world,
    IServiceRegistry services,
    ISchedule schedule,
    IEventBus events) : IPluginContext
{
    public IWorld World { get; } = world;
    public IServiceRegistry Services { get; } = services;
    public ISchedule Schedule { get; } = schedule;
    public IEventBus Events { get; } = events;
    public ILogger Log { get; } = new ConsoleLogger(pluginId);
}
