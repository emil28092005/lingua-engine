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

        // A real cross-ALC subscription, not just a system — this is what
        // makes the 200-cycle leak test in AlcUnloadTests actually prove
        // EventBus.RemoveAllFrom works, the same way registering Tick
        // above proves it for Schedule.RemoveAllFrom.
        ctx.Events.Subscribe<PluginLoaded>(OnPluginLoaded);

        // There's no scene format yet (that's M2) — nothing else will ever
        // put a GameObject in front of `engine run --headless`, so this
        // deliberately-a-test-fixture plugin seeds its own. A real plugin
        // wouldn't do this; scene content isn't a subsystem's job.
        ctx.World.CreateGameObject("sandbox.echo:ping").AddComponent<Ping>();

        ctx.Log.Info("sandbox.echo configured");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("sandbox.echo");
        ctx.Events.RemoveAllFrom("sandbox.echo");
    }

    static void Tick(IWorld world)
    {
        foreach (var go in world.Query<Ping>())
            go.GetComponent<Ping>()!.Count++;
    }

    static void OnPluginLoaded(PluginLoaded evt) =>
        Console.WriteLine($"[sandbox.echo] observed load of '{evt.PluginId}'");
}
