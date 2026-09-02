using System.Numerics;
using Engine.Audio.Contracts;
using Engine.Input.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;
using Engine.Physics.Contracts;
using PhysicsDemoGame;
using Silk.NET.Input;

namespace PhysicsDemoGame.Tests;

// No real window, no real GLFW keyboard, no real Box3D/miniaudio needed to
// test this plugin's OWN logic (spawn-on-press, landed-detection) — only
// whether it calls its three service dependencies correctly. Real OS-level
// key injection isn't available in this sandbox (no xdotool/ydotool under
// Wayland) — a fake IEngineInput the test drives directly is the honest
// substitute, not a compromise: it exercises PhysicsDemoGamePlugin's real
// Configure()/Tick() through the real Schedule (so SystemAccessScope's
// Writes<Rigidbody/BoxCollider/CubeRenderer>() declarations are genuinely
// checked), with IPhysicsService/IAudioService swapped for controllable
// fakes since engine.physics/engine.audio's own correctness is already
// covered by their own test projects.

internal sealed class FakeInput : IEngineInput
{
    public bool SpaceDown;
    public bool IsKeyDown(Key key) => key == Key.Space && SpaceDown;
    public IInputContext Native => throw new NotSupportedException();
}

internal sealed class FakePhysics : IPhysicsService
{
    public readonly Dictionary<GameObject, Vector3> Velocity = [];

    public void ApplyLinearImpulse(GameObject go, Vector3 impulse, bool wake = true) { }
    public Vector3 GetLinearVelocity(GameObject go) => Velocity.GetValueOrDefault(go, Vector3.Zero);
    public void SetLinearVelocity(GameObject go, Vector3 velocity) => Velocity[go] = velocity;
}

internal sealed class FakeAudio : IAudioService
{
    public readonly List<GameObject> Played = [];

    public void Play(GameObject go) => Played.Add(go);
    public void Stop(GameObject go) { }
    public bool IsPlaying(GameObject go) => false;
}

internal sealed class NullLogger : ILogger
{
    public void Info(string message) { }
    public void Warn(string message) { }
    public void Error(string message) { }
}

internal sealed class TestPluginContext(
    IWorld world, IServiceRegistry services, ISchedule schedule, IEventBus events, ITime time) : IPluginContext
{
    public IWorld World { get; } = world;
    public IServiceRegistry Services { get; } = services;
    public ISchedule Schedule { get; } = schedule;
    public IEventBus Events { get; } = events;
    public ILogger Log { get; } = new NullLogger();
    public ITime Time { get; } = time;
}

public class PhysicsDemoGamePluginTests
{
    private static (GameWorld World, Schedule Schedule, FakeInput Input, FakePhysics Physics, FakeAudio Audio) Setup()
    {
        var world = new GameWorld();
        var schedule = new Schedule();
        var services = new ServiceRegistry();
        var input = new FakeInput();
        var physics = new FakePhysics();
        var audio = new FakeAudio();

        services.Provide<IEngineInput>(input);
        services.Provide<IPhysicsService>(physics);
        services.Provide<IAudioService>(audio);

        var ctx = new TestPluginContext(world, services, schedule, new EventBus(), new Time());
        new global::PhysicsDemoGame.PhysicsDemoGamePlugin().Configure(ctx);

        return (world, schedule, input, physics, audio);
    }

    [Fact]
    public void PressingSpace_SpawnsExactlyOneBox_EvenIfHeldAcrossFrames()
    {
        var (world, schedule, input, _, _) = Setup();

        input.SpaceDown = true;
        schedule.RunStage(Stage.Update, world); // press
        schedule.RunStage(Stage.Update, world); // still held — no second spawn

        Assert.Single(world.Roots);
    }

    [Fact]
    public void ReleasingAndPressingAgain_SpawnsASecondBox()
    {
        var (world, schedule, input, _, _) = Setup();

        input.SpaceDown = true;
        schedule.RunStage(Stage.Update, world);
        input.SpaceDown = false;
        schedule.RunStage(Stage.Update, world);
        input.SpaceDown = true;
        schedule.RunStage(Stage.Update, world);

        Assert.Equal(2, world.Roots.Count);
    }

    [Fact]
    public void SpawnedBox_HasRigidbodyBoxColliderAndCubeRenderer()
    {
        var (world, schedule, input, _, _) = Setup();

        input.SpaceDown = true;
        schedule.RunStage(Stage.Update, world);

        var box = Assert.Single(world.Roots);
        Assert.NotNull(box.GetComponent<Rigidbody>());
        Assert.NotNull(box.GetComponent<BoxCollider>());
        Assert.Equal(BodyType.Dynamic, box.GetComponent<Rigidbody>()!.Type);
    }

    [Fact]
    public void BoxThatNeverActuallyFalls_DoesNotTriggerTheBounceSound()
    {
        var (world, schedule, input, physics, audio) = Setup();
        var bounce = world.CreateGameObject("Bounce");

        input.SpaceDown = true;
        schedule.RunStage(Stage.Update, world);
        var box = world.Roots.First(go => go != bounce);

        // Spawned at rest (velocity never set below the falling threshold)
        // — must not be mistaken for "landed."
        for (var i = 0; i < 5; i++)
            schedule.RunStage(Stage.Update, world);

        Assert.Empty(audio.Played);
    }

    [Fact]
    public void BoxThatFallsThenSettles_TriggersTheBounceSoundExactlyOnce()
    {
        var (world, schedule, input, physics, audio) = Setup();
        var bounce = world.CreateGameObject("Bounce");

        input.SpaceDown = true;
        schedule.RunStage(Stage.Update, world);
        input.SpaceDown = false;
        var box = world.Roots.First(go => go != bounce);

        physics.Velocity[box] = new Vector3(0, -5, 0); // falling fast
        schedule.RunStage(Stage.Update, world);
        Assert.Empty(audio.Played);

        physics.Velocity[box] = new Vector3(0, 0, 0); // settled
        schedule.RunStage(Stage.Update, world);
        Assert.Single(audio.Played);
        Assert.Same(bounce, audio.Played[0]);

        // Still resting on later frames — must not re-trigger.
        schedule.RunStage(Stage.Update, world);
        schedule.RunStage(Stage.Update, world);
        Assert.Single(audio.Played);
    }
}
