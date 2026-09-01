using System.Numerics;

namespace Engine.Kernel.World;

/// <summary>
/// Embedded directly on <see cref="GameObject"/> rather than modeled as a
/// <see cref="Component"/> subclass, because nearly every system touches it
/// every frame — see the kernel scope table in docs/kernel-contract.md §2.
///
/// Holds local position/rotation/scale only. World-space composition lives
/// on <see cref="GameObject.WorldMatrix"/>, not here — see that property
/// for why it isn't cached on this struct instead.
/// </summary>
public struct Transform
{
    public Vector3 LocalPosition;
    public Quaternion LocalRotation;
    public Vector3 LocalScale;

    public static Transform Identity => new()
    {
        LocalPosition = Vector3.Zero,
        LocalRotation = Quaternion.Identity,
        LocalScale = Vector3.One,
    };

    public readonly Matrix4x4 LocalMatrix =>
        Matrix4x4.CreateScale(LocalScale) *
        Matrix4x4.CreateFromQuaternion(LocalRotation) *
        Matrix4x4.CreateTranslation(LocalPosition);
}
