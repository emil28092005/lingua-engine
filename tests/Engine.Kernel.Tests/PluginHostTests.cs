using Engine.Kernel.Events;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

namespace Engine.Kernel.Tests;

/// <summary>
/// Edge cases that don't need a real, compiled plugin — see
/// Engine.ConformanceHarness/AlcUnloadTests.cs for the load/unload/reload
/// path exercised against a real one (Sandbox.Echo).
/// </summary>
public class PluginHostTests
{
    private static PluginHost NewHost() =>
        new(new GameWorld(), new ServiceRegistry(), new Schedule(), new NullEventBus());

    [Fact]
    public void Load_Throws_When_The_Directory_Has_No_Manifest()
    {
        var host = NewHost();
        var emptyDir = Directory.CreateTempSubdirectory();

        try
        {
            Assert.Throws<FileNotFoundException>(() => host.Load(emptyDir.FullName));
        }
        finally
        {
            emptyDir.Delete(recursive: true);
        }
    }
}
