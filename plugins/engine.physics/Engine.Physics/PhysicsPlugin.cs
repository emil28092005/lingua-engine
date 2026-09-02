using System.Numerics;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Engine.Physics.Contracts;

namespace Engine.Physics;

/// <summary>
/// M4's physics plugin: Box3D, over the shim in native/physics-native/ —
/// see that file's own doc comment for why nothing crosses the P/Invoke
/// boundary but plain scalars. One system, on Stage.FixedUpdate: sync new/
/// removed Rigidbodies, step once, write results back to Transform. Each
/// FixedUpdate invocation is exactly one physics step at ITime.
/// FixedDeltaTime — Engine.Host's accumulator (Time.ConsumeFixedSteps)
/// decides how many times that runs this frame, not this plugin.
/// </summary>
public sealed class PhysicsPlugin : IPlugin
{
    private static readonly Vector3 Gravity = new(0f, -9.81f, 0f);

    private PhysicsWorld? _world;
    private ITime? _time;

    public void Configure(IPluginContext ctx)
    {
        _time = ctx.Time;
        _world = new PhysicsWorld(Gravity, ctx.Log);
        ctx.Services.Provide<IPhysicsService>(new PhysicsService(_world));

        ctx.Schedule.Add(Stage.FixedUpdate, Step)
            .Reads<Rigidbody>()
            .Reads<BoxCollider>()
            .Reads<SphereCollider>();

        ctx.Log.Info("physics world ready (Box3D)");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.physics");
        ctx.Services.Revoke<IPhysicsService>();
        _world?.Dispose();
        _world = null;
        _time = null;
    }

    private void Step(IWorld world)
    {
        _world!.Sync(world);
        _world.Step(_time!.FixedDeltaTime);
    }
}
