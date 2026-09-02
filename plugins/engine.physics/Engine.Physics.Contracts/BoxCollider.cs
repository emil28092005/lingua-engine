using System.Numerics;
using Engine.Kernel.World;

namespace Engine.Physics.Contracts;

/// <summary>
/// A box collision shape, sized in half-extents (distance from center to
/// each face) — Box3D's own convention (b3MakeBoxHull's hx/hy/hz), kept
/// as-is rather than doubled into a "full size" field so there's no /2
/// conversion to get subtly wrong between here and the native shim.
/// </summary>
public sealed class BoxCollider : Component
{
    public Vector3 HalfExtents = new(0.5f, 0.5f, 0.5f);
}
