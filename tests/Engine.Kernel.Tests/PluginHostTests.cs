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

    [Fact]
    public void LoadProject_Throws_When_The_Project_Manifest_Is_Missing()
    {
        var host = NewHost();
        var missingPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}-project.json");

        Assert.Throws<FileNotFoundException>(() => host.LoadProject(missingPath, []));
    }

    [Fact]
    public void LoadProject_Throws_When_A_Referenced_Plugin_Is_Not_Found_In_Any_Search_Path()
    {
        var host = NewHost();
        var dir = Directory.CreateTempSubdirectory();

        try
        {
            var projectPath = Path.Combine(dir.FullName, "project.json");
            File.WriteAllText(projectPath,
                """{ "engineVersion": "^0.1", "plugins": [ { "id": "nonexistent.plugin" } ] }""");

            Assert.Throws<InvalidOperationException>(() => host.LoadProject(projectPath, [dir.FullName]));
        }
        finally
        {
            dir.Delete(recursive: true);
        }
    }
}
