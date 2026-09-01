using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Sandbox.Echo.Contracts;

namespace Sandbox.Echo;

/// <summary>
/// M0's test fixture. Exists to exercise the full loop end to end — plugin
/// load, a real cross-ALC system registered and invoked by Schedule, and
/// hot reload — before any real subsystem exists to test any of it
/// against. See M0 in docs/kernel-contract.md §8.
/// </summary>
public sealed class EchoPlugin : IPlugin
{
    public void Configure(IPluginContext ctx)
    {
        ctx.Schedule.Add(Stage.Update, Tick)
           .Writes<Ping>();

        ctx.Log.Info("sandbox.echo configured");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("sandbox.echo");
    }

    static void Tick(IWorld world)
    {
        foreach (var go in world.Query<Ping>())
            go.GetComponent<Ping>()!.Count++;
    }
}
