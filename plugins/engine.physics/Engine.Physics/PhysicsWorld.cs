using System.Numerics;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.World;
using Engine.Physics.Contracts;

namespace Engine.Physics;

/// <summary>
/// Owns the one native Box3D world this plugin creates, and the GameObject
/// &lt;-&gt; native body handle mapping. <see cref="Sync"/> creates bodies for
/// any GameObject that's grown a Rigidbody since last frame and destroys
/// bodies for any that lost one (component removed, or the GameObject
/// itself was destroyed) — there's no destruction event to hook, so this
/// diffs Query&lt;Rigidbody&gt;() against the tracked set every FixedUpdate
/// instead. <see cref="Step"/> advances the simulation once and writes
/// every tracked body's new transform back into its GameObject.
/// </summary>
internal sealed class PhysicsWorld : IDisposable
{
    private readonly int _handle;
    private readonly ILogger _log;
    private readonly Dictionary<GameObject, int> _bodies = [];
    private readonly HashSet<GameObject> _warnedMissingCollider = [];

    public PhysicsWorld(Vector3 gravity, ILogger log)
    {
        _log = log;
        _handle = Native.Lingua_CreateWorld(gravity.X, gravity.Y, gravity.Z);
    }

    public void Sync(IWorld world)
    {
        var live = new HashSet<GameObject>();

        foreach (var go in world.Query<Rigidbody>())
        {
            live.Add(go);
            if (!_bodies.ContainsKey(go))
                TryCreateBody(go);
        }

        if (_bodies.Count == live.Count)
            return;

        foreach (var stale in _bodies.Keys.Where(go => !live.Contains(go)).ToList())
        {
            Native.Lingua_DestroyBody(_bodies[stale]);
            _bodies.Remove(stale);
            _warnedMissingCollider.Remove(stale);
        }
    }

    public void Step(float timeStep, int subStepCount = 4)
    {
        Native.Lingua_WorldStep(_handle, timeStep, subStepCount);

        foreach (var (go, handle) in _bodies)
        {
            Native.Lingua_GetBodyTransform(handle, out var px, out var py, out var pz, out var qx, out var qy, out var qz, out var qw);

            var t = go.Transform;
            t.LocalPosition = new Vector3(px, py, pz);
            t.LocalRotation = new Quaternion(qx, qy, qz, qw);
            go.Transform = t;
        }
    }

    public void ApplyLinearImpulse(GameObject go, Vector3 impulse, bool wake)
    {
        if (_bodies.TryGetValue(go, out var handle))
            Native.Lingua_ApplyLinearImpulse(handle, impulse.X, impulse.Y, impulse.Z, wake);
    }

    public Vector3 GetLinearVelocity(GameObject go)
    {
        if (!_bodies.TryGetValue(go, out var handle))
            return Vector3.Zero;

        Native.Lingua_GetLinearVelocity(handle, out var vx, out var vy, out var vz);
        return new Vector3(vx, vy, vz);
    }

    public void SetLinearVelocity(GameObject go, Vector3 velocity)
    {
        if (_bodies.TryGetValue(go, out var handle))
            Native.Lingua_SetLinearVelocity(handle, velocity.X, velocity.Y, velocity.Z);
    }

    private void TryCreateBody(GameObject go)
    {
        var rb = go.GetComponent<Rigidbody>()!;
        var pos = go.Transform.LocalPosition;
        var rot = go.Transform.LocalRotation;

        var box = go.GetComponent<BoxCollider>();
        var sphere = go.GetComponent<SphereCollider>();

        int handle;
        if (box is not null)
        {
            handle = Native.Lingua_CreateBoxBody(
                _handle, pos.X, pos.Y, pos.Z, rot.X, rot.Y, rot.Z, rot.W,
                box.HalfExtents.X, box.HalfExtents.Y, box.HalfExtents.Z,
                (int)rb.Type, rb.Density, rb.Friction, rb.Restitution);
        }
        else if (sphere is not null)
        {
            handle = Native.Lingua_CreateSphereBody(
                _handle, pos.X, pos.Y, pos.Z, rot.X, rot.Y, rot.Z, rot.W,
                sphere.Radius, (int)rb.Type, rb.Density, rb.Friction, rb.Restitution);
        }
        else
        {
            if (_warnedMissingCollider.Add(go))
                _log.Warn($"'{go.Name}' has a Rigidbody but no BoxCollider/SphereCollider — no physics body created.");
            return;
        }

        _bodies[go] = handle;
    }

    public void Dispose()
    {
        foreach (var handle in _bodies.Values)
            Native.Lingua_DestroyBody(handle);

        _bodies.Clear();
        Native.Lingua_DestroyWorld(_handle);
    }
}
