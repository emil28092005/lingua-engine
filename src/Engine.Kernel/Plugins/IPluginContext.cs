using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

namespace Engine.Kernel.Plugins;

public interface IPluginContext
{
    IWorld World { get; }             // data
    IServiceRegistry Services { get; } // Provide<T> / Require<T>
    ISchedule Schedule { get; }        // systems and ordering
    IEventBus Events { get; }
    ILogger Log { get; }
}
