using System.Runtime.Loader;
using Engine.Kernel.Events;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

namespace Engine.ConformanceHarness;

/// <summary>
/// The test docs/kernel-contract.md §4 calls the one thing that keeps this
/// architecture from slowly degrading: load and unload a plugin 200 times,
/// and after every cycle verify the ALC actually collected. Runs against
/// Sandbox.Echo — see the ReferenceOutputAssembly="false" note in this
/// project's .csproj for why that reference doesn't link its types in, and
/// the CopyToOutputDirectory items for how its built DLLs end up sitting
/// next to plugin.json under this project's own output.
/// </summary>
public class AlcUnloadTests
{
    private static string PluginDirectory =>
        Path.Combine(AppContext.BaseDirectory, "plugins", "sandbox.echo");

    private static PluginHost NewHost() =>
        new(new GameWorld(), new ServiceRegistry(), new Schedule(), new NullEventBus());

    [Fact]
    public void Plugin_Survives_200_Load_Unload_Cycles()
    {
        var host = NewHost();

        for (var i = 0; i < 200; i++)
        {
            var id = host.Load(PluginDirectory);
            var weakAlc = host.Unload(id);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Assert.False(weakAlc.IsAlive, $"ALC survived unload cycle {i}.");
        }

        // The WeakReference check above only proves the collectible ALC
        // let go. It says nothing about the Default ALC, which is never
        // supposed to grow at all across reloads — Contracts loads once
        // and every later cycle should find it already there. A count
        // above 1 here would be a real, separate leak this test would
        // otherwise miss entirely.
        var contractsCopies = AssemblyLoadContext.Default.Assemblies
            .Count(a => a.GetName().Name == "Sandbox.Echo.Contracts");
        Assert.Equal(1, contractsCopies);
    }

    [Fact]
    public void Load_Configures_The_Plugin_Without_Throwing()
    {
        var host = NewHost();

        var id = host.Load(PluginDirectory);

        Assert.Equal("sandbox.echo", id);
    }

    [Fact]
    public void Load_Throws_When_The_Same_Plugin_Is_Already_Loaded()
    {
        var host = NewHost();
        host.Load(PluginDirectory);

        Assert.Throws<InvalidOperationException>(() => host.Load(PluginDirectory));
    }

    [Fact]
    public void Unload_Throws_When_The_Plugin_Was_Never_Loaded()
    {
        var host = NewHost();

        Assert.Throws<InvalidOperationException>(() => host.Unload("sandbox.echo"));
    }
}
