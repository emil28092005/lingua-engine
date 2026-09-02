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
}
