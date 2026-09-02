using System.Diagnostics;
using Engine.Kernel.World;

namespace Engine.Kernel.Tests;

public class WorldSnapshotTests
{
    private sealed class Health : Component
    {
        public int Value;
    }

    [Fact]
    public void Restore_Discards_Mutations_Made_After_The_Snapshot()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("Hero");
        go.AddComponent<Health>().Value = 100;

        var snapshot = world.Snapshot();

        go.GetComponent<Health>()!.Value = 1; // "took damage" during Play
        world.CreateGameObject("SpawnedDuringPlay");

        world.Restore(snapshot);

        var root = Assert.Single(world.Roots);
        Assert.Equal("Hero", root.Name);
        Assert.Equal(100, root.GetComponent<Health>()!.Value);
    }

    [Fact]
    public void Restore_Removes_GameObjects_Created_After_The_Snapshot()
    {
        var world = new GameWorld();
        world.CreateGameObject("Original");

        var snapshot = world.Snapshot();
        world.CreateGameObject("Spawned");

        world.Restore(snapshot);

        Assert.Single(world.Roots);
        Assert.Equal("Original", world.Roots[0].Name);
    }

    [Fact]
    public void Restore_Recreates_GameObjects_Destroyed_After_The_Snapshot()
    {
        var world = new GameWorld();
        world.CreateGameObject("WillBeDestroyed");
        var snapshot = world.Snapshot();

        world.Destroy(world.Roots[0]);
        Assert.Empty(world.Roots);

        world.Restore(snapshot);

        Assert.Single(world.Roots);
        Assert.Equal("WillBeDestroyed", world.Roots[0].Name);
    }

    [Fact]
    public void Restore_Preserves_The_Parent_Child_Hierarchy()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");
        child.SetParent(parent);
        var snapshot = world.Snapshot();

        child.SetParent(null); // detach during Play

        world.Restore(snapshot);

        var root = Assert.Single(world.Roots);
        var restoredChild = Assert.Single(root.Children);
        Assert.Equal("Child", restoredChild.Name);
    }

    // M3's actual "done when": entering Play takes under 100 ms. A
    // realistic indie-scale scene (a few hundred GameObjects, each with a
    // component) should clear that with room to spare — this asserts a
    // generous 100 ms bound end to end (snapshot + restore, i.e. both
    // EnterPlay and ExitPlay), not a tight one that would make this test
    // flaky on a loaded CI box for no reason.
    [Fact]
    public void Snapshot_And_Restore_A_Few_Hundred_GameObjects_Well_Under_100ms()
    {
        var world = new GameWorld();
        for (var i = 0; i < 300; i++)
        {
            var go = world.CreateGameObject($"Object{i}");
            go.AddComponent<Health>().Value = i;
        }

        var stopwatch = Stopwatch.StartNew();
        var snapshot = world.Snapshot();
        world.Restore(snapshot);
        stopwatch.Stop();

        Assert.True(
            stopwatch.ElapsedMilliseconds < 100,
            $"Snapshot + Restore of 300 GameObjects took {stopwatch.ElapsedMilliseconds} ms, expected < 100 ms.");
    }
}
