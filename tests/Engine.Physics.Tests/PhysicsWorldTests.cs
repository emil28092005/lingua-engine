using System.Numerics;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.World;
using Engine.Physics;
using Engine.Physics.Contracts;

namespace Engine.Physics.Tests;

file sealed class RecordingLogger : ILogger
{
    public List<string> Warnings { get; } = [];

    public void Info(string message) { }
    public void Warn(string message) => Warnings.Add(message);
    public void Error(string message) { }
}

public class PhysicsWorldTests
{
    private static GameObject CreateGround(GameWorld world)
    {
        var go = world.CreateGameObject("Ground");
        go.Transform = Transform.Identity;
        go.AddComponent<Rigidbody>().Type = BodyType.Static;
        go.AddComponent<BoxCollider>().HalfExtents = new Vector3(10f, 0.5f, 10f);
        return go;
    }

    [Fact]
    public void DynamicBox_FallsAndSettlesOnStaticGround()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(new Vector3(0, -10, 0), log);

        CreateGround(world);

        var box = world.CreateGameObject("FallingBox");
        box.Transform = Transform.Identity;
        box.Transform.LocalPosition = new Vector3(0, 5, 0);
        box.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        box.AddComponent<BoxCollider>().HalfExtents = new Vector3(0.5f, 0.5f, 0.5f);

        for (var i = 0; i < 150; i++)
        {
            physics.Sync(world);
            physics.Step(1f / 50f);
        }

        // Ground half-height 0.5 + box half-height 0.5 = rests at y=1.
        Assert.Equal(1f, box.Transform.LocalPosition.Y, 1);
        Assert.Empty(log.Warnings);
    }

    [Fact]
    public void DynamicSphere_FallsAndSettlesOnStaticGround()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(new Vector3(0, -10, 0), log);

        CreateGround(world);

        var sphere = world.CreateGameObject("FallingSphere");
        sphere.Transform = Transform.Identity;
        sphere.Transform.LocalPosition = new Vector3(0, 5, 0);
        sphere.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        sphere.AddComponent<SphereCollider>().Radius = 0.5f;

        for (var i = 0; i < 150; i++)
        {
            physics.Sync(world);
            physics.Step(1f / 50f);
        }

        Assert.Equal(1f, sphere.Transform.LocalPosition.Y, 1);
    }

    [Fact]
    public void RigidbodyWithoutCollider_WarnsOnceAndDoesNotThrow()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(new Vector3(0, -10, 0), log);

        var go = world.CreateGameObject("NoShape");
        go.Transform = Transform.Identity;
        go.AddComponent<Rigidbody>();

        for (var i = 0; i < 5; i++)
        {
            physics.Sync(world);
            physics.Step(1f / 50f);
        }

        Assert.Single(log.Warnings);
    }

    [Fact]
    public void DestroyedGameObject_BodyCleanedUp_SubsequentStepsStillWork()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(new Vector3(0, -10, 0), log);

        CreateGround(world);

        var box = world.CreateGameObject("Temp");
        box.Transform = Transform.Identity;
        box.Transform.LocalPosition = new Vector3(0, 5, 0);
        box.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        box.AddComponent<BoxCollider>();

        physics.Sync(world);
        physics.Step(1f / 50f);

        world.Destroy(box);

        // Should not throw even though the body backing a now-destroyed
        // GameObject still existed in the tracking table until this Sync.
        for (var i = 0; i < 10; i++)
        {
            physics.Sync(world);
            physics.Step(1f / 50f);
        }
    }

    [Fact]
    public void ApplyLinearImpulse_ChangesVelocity()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(Vector3.Zero, log); // no gravity, isolate the impulse

        var box = world.CreateGameObject("Box");
        box.Transform = Transform.Identity;
        box.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        box.AddComponent<BoxCollider>();

        physics.Sync(world); // create the body before applying an impulse to it

        physics.ApplyLinearImpulse(box, new Vector3(5, 0, 0), wake: true);

        var velocity = physics.GetLinearVelocity(box);
        Assert.True(velocity.X > 0f, $"expected positive X velocity after impulse, got {velocity.X}");
    }

    // Sync's old early-return compared _bodies.Count to live.Count, not
    // their contents — a real leak found by independent review, not by
    // any of the tests above (all of them either never remove a
    // GameObject, or do so in a way that changes the count). Both tests
    // below reproduce the review's own two scenarios and assert on
    // Native.Lingua_GetBodyCount() directly: the native table's own count
    // is what actually leaks, and PhysicsWorld's C#-side _bodies
    // dictionary alone can't prove it didn't.

    [Fact]
    public void Sync_DestroysStaleBody_EvenWhenLiveCountStaysTheSame()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(new Vector3(0, -10, 0), log);

        var a = world.CreateGameObject("A");
        a.Transform = Transform.Identity;
        a.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        a.AddComponent<BoxCollider>();

        // B has a Rigidbody but no collider — it counts toward "live"
        // every Sync (any Rigidbody does) but never gets a native body.
        var b = world.CreateGameObject("B");
        b.Transform = Transform.Identity;
        b.AddComponent<Rigidbody>().Type = BodyType.Dynamic;

        physics.Sync(world);
        Assert.Equal(1, Native.Lingua_GetBodyCount());

        world.Destroy(a);
        // live = {B} (1), stale _bodies = {A} (1) — same count as before
        // destroying A, which is exactly what let the old bug's early
        // return skip cleanup.
        physics.Sync(world);

        Assert.Equal(0, Native.Lingua_GetBodyCount());
    }

    [Fact]
    public void Sync_DestroysStaleBody_AcrossARestore()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var physics = new PhysicsWorld(new Vector3(0, -10, 0), log);

        var a = world.CreateGameObject("A");
        a.Transform = Transform.Identity;
        a.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        a.AddComponent<BoxCollider>();

        var b = world.CreateGameObject("B"); // Rigidbody, no collider
        b.Transform = Transform.Identity;
        b.AddComponent<Rigidbody>().Type = BodyType.Dynamic;

        physics.Sync(world);
        Assert.Equal(1, Native.Lingua_GetBodyCount());

        // Restore destroys A and B and rebuilds fresh instances A'/B' from
        // the snapshot. The next Sync creates A' (a new body) while old
        // A's body is now orphaned — _bodies briefly holds {A, A'} (2)
        // and live holds {A', B'} (2), the same count, which is exactly
        // what the old bug's early return let slip through as "nothing
        // stale to clean up."
        var snapshot = world.Snapshot();
        world.Restore(snapshot);
        physics.Sync(world);

        // Exactly one live native body (A'), not two (leaked old A + A').
        Assert.Equal(1, Native.Lingua_GetBodyCount());
    }
}
