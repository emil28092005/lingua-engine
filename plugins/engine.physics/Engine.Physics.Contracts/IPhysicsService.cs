using System.Numerics;
using Engine.Kernel.World;

namespace Engine.Physics.Contracts;

/// <summary>
/// Gameplay-facing physics actions — impulses and velocity — for a
/// GameObject that already has a Rigidbody. Separate from just poking
/// Rigidbody's own fields because these aren't state the Inspector should
/// show as persistent data (a velocity is a live simulation value, not
/// something a scene file should round-trip) and because applying an
/// impulse isn't a field write at all, it's an action Box3D performs.
/// </summary>
public interface IPhysicsService
{
    void ApplyLinearImpulse(GameObject go, Vector3 impulse, bool wake = true);

    Vector3 GetLinearVelocity(GameObject go);

    void SetLinearVelocity(GameObject go, Vector3 velocity);
}
