using System.Numerics;
using Engine.Audio.Contracts;
using Engine.Input.Contracts;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Engine.Physics.Contracts;
using Engine.Render.Contracts;
using Silk.NET.Input;

namespace PhysicsDemoGame;

/// <summary>
/// M4's "one small game, end to end": pressing Space drops a new box from
/// above the ground; engine.physics simulates it falling and settling;
/// engine.render draws it as a real cube (CubeRenderer, added specifically
/// for this); this plugin plays a bounce sound (engine.audio) the moment
/// each box's fall actually stops. Physics, audio, input, and rendering
/// all cooperate here through nothing but the kernel's own vocabulary —
/// this plugin never references engine.physics/audio/render's
/// implementation assemblies, only their Contracts.
///
/// "Landed" isn't a Box3D contact event — the native shim doesn't expose
/// one (see native/physics-native/lingua_physics.c's own doc comment on
/// why nothing but scalars crosses that boundary; a contact callback would
/// need to carry structured per-contact data across it, exactly what's
/// kept out). Honest scope for a demo this size: watch each spawned box's
/// own Y velocity via IPhysicsService every frame, and call it landed the
/// first time a real fall (velocity past FallingThreshold at some point)
/// settles back near zero.
/// </summary>
public sealed class PhysicsDemoGamePlugin : IPlugin
{
    private const float SpawnHeight = 6f;
    private const float FallingThreshold = -1f;
    private const float RestThreshold = 0.3f;

    private readonly Random _random = new();
    private readonly List<GameObject> _spawned = [];
    private readonly HashSet<GameObject> _hasFallen = [];
    private readonly HashSet<GameObject> _landed = [];

    private IEngineInput? _input;
    private IPhysicsService? _physics;
    private IAudioService? _audio;
    private GameObject? _bounceSound;
    private bool _spacePressedLastFrame;

    public void Configure(IPluginContext ctx)
    {
        _input = ctx.Services.Require<IEngineInput>();
        _physics = ctx.Services.Require<IPhysicsService>();
        _audio = ctx.Services.Require<IAudioService>();

        ctx.Schedule.Add(Stage.Update, Tick)
            .Writes<Rigidbody>()
            .Writes<BoxCollider>()
            .Writes<CubeRenderer>();

        ctx.Log.Info("physics demo ready — press Space to drop a box");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("physics-demo-game");
        _spawned.Clear();
        _hasFallen.Clear();
        _landed.Clear();
        _bounceSound = null;
        _input = null;
        _physics = null;
        _audio = null;
    }

    private void Tick(IWorld world)
    {
        _bounceSound ??= world.Roots.FirstOrDefault(go => go.Name == "Bounce");

        var spacePressed = _input!.IsKeyDown(Key.Space);
        if (spacePressed && !_spacePressedLastFrame)
            Spawn(world);
        _spacePressedLastFrame = spacePressed;

        foreach (var box in _spawned)
        {
            if (_landed.Contains(box))
                continue;

            var velocityY = _physics!.GetLinearVelocity(box).Y;

            if (velocityY < FallingThreshold)
            {
                _hasFallen.Add(box);
                continue;
            }

            if (_hasFallen.Contains(box) && MathF.Abs(velocityY) < RestThreshold)
            {
                _landed.Add(box);
                if (_bounceSound is not null)
                    _audio!.Play(_bounceSound);
            }
        }
    }

    private void Spawn(IWorld world)
    {
        var go = world.CreateGameObject($"Box{_spawned.Count}");
        go.Transform = Transform.Identity;
        go.Transform.LocalPosition = new Vector3(
            (float)(_random.NextDouble() * 4 - 2), SpawnHeight, (float)(_random.NextDouble() * 4 - 2));

        go.AddComponent<Rigidbody>().Type = BodyType.Dynamic;
        go.AddComponent<BoxCollider>();
        go.AddComponent<CubeRenderer>();

        _spawned.Add(go);
    }
}
