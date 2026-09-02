using Engine.Kernel.World;

namespace Engine.Physics.Contracts;

/// <summary>
/// Marks a GameObject as physics-simulated. Needs a BoxCollider or
/// SphereCollider on the same GameObject to actually get a body — Rigidbody
/// alone describes how it moves, not what shape it collides as, same split
/// Box3D itself makes between a body and its shapes.
///
/// Physics-enabled GameObjects must be root-level (no parent) for now:
/// PhysicsWorld writes Box3D's world-space transform straight into
/// Transform.LocalPosition/LocalRotation, which is only correct when local
/// and world space are the same thing. A parented rigidbody would need the
/// same parent-WorldMatrix-inverse handling GizmoMath.WorldToLocalPosition
/// already does for the gizmo — real work, not done here because nothing
/// in M4's demo needs a physics object with a parent yet.
/// </summary>
public sealed class Rigidbody : Component
{
    public BodyType Type = BodyType.Dynamic;
    public float Density = 1f;
    public float Friction = 0.5f;
    public float Restitution = 0.1f;
}
