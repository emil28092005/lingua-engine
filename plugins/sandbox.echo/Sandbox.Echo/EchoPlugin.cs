using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Sandbox.Echo.Contracts;

namespace Sandbox.Echo;

/// <summary>
/// M0's test fixture. Exists only to exercise the reload loop end to end —
/// edit, rebuild, ALC reload, headless run, dump — before any real
/// subsystem exists to test it against. See M0 in docs/kernel-contract.md §8.
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
        // TODO(M0): bump every Ping.Count by one. Placeholder until the
        // Scheduler actually exists to invoke this.
    }
}
