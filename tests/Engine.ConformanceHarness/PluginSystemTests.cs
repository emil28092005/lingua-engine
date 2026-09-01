using Engine.Kernel.Events;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;
using Sandbox.Echo.Contracts;

namespace Engine.ConformanceHarness;

/// <summary>
/// Closes the loop this whole exercise was for: a plugin loaded from a real
/// collectible ALC registers a system with Schedule, Schedule actually
/// invokes that cross-ALC delegate, and it correctly mutates a component
/// living in the Default-ALC-owned World. If PluginLoadContext resolved
/// Sandbox.Echo.Contracts incorrectly (a second, distinct copy instead of
/// deferring to the one this project references directly — see the note on
/// that ProjectReference in the .csproj), the Ping instance this test
/// constructs wouldn't be a Ping as far as EchoPlugin's Tick sees it, and
/// world.Query&lt;Ping&gt;() inside the plugin would find nothing.
/// </summary>
public class PluginSystemTests
{
    private static string PluginDirectory =>
        Path.Combine(AppContext.BaseDirectory, "plugins", "sandbox.echo");

    [Fact]
    public void Loaded_Plugin_System_Runs_And_Mutates_A_Component_It_Declared()
    {
        var world = new GameWorld();
        var schedule = new Schedule();
        var host = new PluginHost(world, new ServiceRegistry(), schedule, new NullEventBus());

        var ping = world.CreateGameObject("Pinger").AddComponent<Ping>();
        var id = host.Load(PluginDirectory);

        schedule.RunStage(Stage.Update, world);
        schedule.RunStage(Stage.Update, world);

        Assert.Equal(2, ping.Count);

        host.Unload(id);
    }

    [Fact]
    public void Unloading_The_Plugin_Stops_Its_System_From_Running()
    {
        var world = new GameWorld();
        var schedule = new Schedule();
        var host = new PluginHost(world, new ServiceRegistry(), schedule, new NullEventBus());

        var ping = world.CreateGameObject("Pinger").AddComponent<Ping>();
        var id = host.Load(PluginDirectory);
        host.Unload(id);

        schedule.RunStage(Stage.Update, world);

        Assert.Equal(0, ping.Count);
    }
}
