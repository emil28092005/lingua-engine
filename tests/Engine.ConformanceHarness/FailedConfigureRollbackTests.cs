using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;
using Sandbox.Echo.Contracts;

namespace Engine.ConformanceHarness;

/// <summary>
/// Found by independent review, not by any test: PluginHost.Load didn't
/// roll back anything when Configure threw partway through — Schedule.Add/
/// Events.Subscribe calls it already made stayed registered forever, and
/// the ALC it loaded into was never unloaded. Sandbox.FailingConfigure
/// exists specifically to load real, into a real collectible ALC, and
/// throw after registering a system, exactly reproducing that shape.
///
/// The system it registers increments a shared Ping component — a real,
/// deterministic side effect. An earlier version of this test tried to
/// prove the rollback via AssemblyLoadContext.All instead (did the failed
/// load's ALC get collected?), but that passed even against the
/// deliberately-reverted buggy PluginHost.Load: the ALC turned out to get
/// collected either way once its only references (local variables inside
/// Load) went out of scope, regardless of whether Schedule/EventBus still
/// held onto its assembly. Watching whether the dangling system still
/// fires is the thing that actually distinguishes rolled-back from not.
/// </summary>
public class FailedConfigureRollbackTests
{
    private static string PluginDirectory =>
        Path.Combine(AppContext.BaseDirectory, "plugins", "sandbox.failing-configure");

    private static PluginHost NewHost(Schedule schedule, GameWorld world) =>
        new(world, new ServiceRegistry(), schedule, new EventBus(), new Time());

    [Fact]
    public void Load_Rethrows_The_Configure_Exception()
    {
        var host = NewHost(new Schedule(), new GameWorld());

        var ex = Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));
        Assert.Equal("deliberate failure for PluginHostTests", ex.Message);
    }

    [Fact]
    public void Load_Fails_The_Same_Way_On_Retry_Instead_Of_Already_Loaded()
    {
        var host = NewHost(new Schedule(), new GameWorld());

        Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));

        // Before the fix, _loaded never got an entry either way — this
        // alone wouldn't have caught the bug — but it's still the right
        // thing to be true: retrying isn't "already loaded," it fails the
        // same way every time.
        var ex = Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));
        Assert.Equal("deliberate failure for PluginHostTests", ex.Message);
    }

    [Fact]
    public void IsLoaded_Is_False_After_A_Failed_Load_So_Retry_Does_Not_Need_Unload_First()
    {
        var host = NewHost(new Schedule(), new GameWorld());

        Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));
        Assert.False(host.IsLoaded("sandbox.failing-configure"));

        // Mirrors Engine.Host's own "r <id>" handler: only Unload first if
        // IsLoaded says so. Before that check existed, this exact retry
        // sequence threw "Plugin 'sandbox.failing-configure' is not
        // loaded" instead of ever reaching Load again.
        if (host.IsLoaded("sandbox.failing-configure"))
            host.Unload("sandbox.failing-configure");

        Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));
    }

    [Fact]
    public void Load_Removes_The_System_Configure_Registered_Before_Throwing()
    {
        var world = new GameWorld();
        var schedule = new Schedule();
        var host = NewHost(schedule, world);

        var ping = world.CreateGameObject("Pinger").AddComponent<Ping>();

        Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));

        // Without rollback, the Tick system FailingConfigurePlugin
        // registered before throwing is still sitting in Schedule and
        // fires here, incrementing Count. With rollback (schedule.
        // RemoveAllFrom in the catch block), it's gone, and this does
        // nothing.
        schedule.RunStage(Stage.Update, world);
        schedule.RunStage(Stage.Update, world);

        Assert.Equal(0, ping.Count);
    }
}
