using System.Numerics;

namespace Engine.Kernel.World;

/// <summary>
/// Embedded directly on <see cref="GameObject"/> rather than modeled as a
/// <see cref="Component"/> subclass, because nearly every system touches it
/// every frame — see the kernel scope table in docs/kernel-contract.md §2.
/// </summary>
public struct Transform
{
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 LocalScale;

    // TODO(M0): derive from the GameObject hierarchy once World can walk it.
    public readonly Matrix4x4 WorldMatrix => throw new NotImplementedException();
}
