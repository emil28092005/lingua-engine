using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Sandbox.Echo.Contracts;

namespace Sandbox.FailingConfigure;

/// <summary>
/// A test fixture, not a real plugin — exists only so
/// FailedConfigureRollbackTests can exercise PluginHost.Load's rollback
/// path against a real ALC/assembly load, the same way sandbox.echo exists
/// for the successful-load path. Registers a system that increments a
/// shared Ping component (so there's a real, deterministic side effect for
/// the test to check — did the dangling system actually get removed, not
/// just "did the ALC eventually get GC'd," which turned out to happen
/// either way regardless of whether rollback ran, making it useless as a
/// regression signal here), then throws — reproducing "Configure got
/// partway through before failing," not "Configure failed immediately."
/// </summary>
public sealed class FailingConfigurePlugin : IPlugin
{
    public void Configure(IPluginContext ctx)
    {
        ctx.Schedule.Add(Stage.Update, Tick).Writes<Ping>();
        ctx.Events.Subscribe<PluginLoaded>(_ => { });

        throw new InvalidOperationException("deliberate failure for PluginHostTests");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("sandbox.failing-configure");
        ctx.Events.RemoveAllFrom("sandbox.failing-configure");
    }

    private static void Tick(IWorld world)
    {
        foreach (var go in world.Query<Ping>())
            go.GetComponent<Ping>()!.Count++;
    }
}
