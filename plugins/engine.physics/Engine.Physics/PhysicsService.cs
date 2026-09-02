using System.Numerics;
using Engine.Kernel.World;
using Engine.Physics.Contracts;

namespace Engine.Physics;

internal sealed class PhysicsService(PhysicsWorld world) : IPhysicsService
{
    public void ApplyLinearImpulse(GameObject go, Vector3 impulse, bool wake = true) =>
        world.ApplyLinearImpulse(go, impulse, wake);

    public Vector3 GetLinearVelocity(GameObject go) => world.GetLinearVelocity(go);

    public void SetLinearVelocity(GameObject go, Vector3 velocity) => world.SetLinearVelocity(go, velocity);
}
