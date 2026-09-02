namespace Engine.Physics.Contracts;

/// <summary>
/// Values match Box3D's own b3BodyType exactly (Static=0, Kinematic=1,
/// Dynamic=2) so engine.physics can cast this straight into the native
/// shim's bodyType parameter with no translation table to keep in sync.
/// </summary>
public enum BodyType
{
    Static = 0,
    Kinematic = 1,
    Dynamic = 2,
}
