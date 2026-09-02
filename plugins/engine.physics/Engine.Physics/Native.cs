using System.Runtime.InteropServices;

namespace Engine.Physics;

/// <summary>
/// The entire P/Invoke surface, matching native/physics-native/
/// lingua_physics.c's exports one-to-one. Every parameter is a plain
/// int/float/bool — no struct crosses this boundary, on purpose, see that
/// file's own doc comment for why.
///
/// Classic DllImport, not the newer source-generated LibraryImport:
/// LibraryImport's generated marshalling code needs AllowUnsafeBlocks even
/// for an all-blittable signature like every one of these, and there's no
/// reason to open that door — DllImport marshals plain int/float/bool/out
/// float scalars with no unsafe code at all, which is what actually keeps
/// this plugin inside the kernel's "no unsafe in the v1 hot path" rule
/// (Directory.Build.props) instead of needing a narrow exception to it.
/// </summary>
internal static class Native
{
    private const string Lib = "lingua_physics";

    [DllImport(Lib)]
    public static extern int Lingua_CreateWorld(float gravityX, float gravityY, float gravityZ);

    [DllImport(Lib)]
    public static extern void Lingua_DestroyWorld(int worldHandle);

    [DllImport(Lib)]
    public static extern void Lingua_WorldStep(int worldHandle, float timeStep, int subStepCount);

    /// <summary>Total live bodies across every world — a test-only escape
    /// hatch (see PhysicsWorldTests) to assert on the native side's own
    /// bookkeeping, not just PhysicsWorld's C#-side dictionary.</summary>
    [DllImport(Lib)]
    public static extern int Lingua_GetBodyCount();

    [DllImport(Lib)]
    public static extern int Lingua_CreateBoxBody(
        int worldHandle,
        float px, float py, float pz,
        float qx, float qy, float qz, float qw,
        float halfWidth, float halfHeight, float halfDepth,
        int bodyType, float density, float friction, float restitution);

    [DllImport(Lib)]
    public static extern int Lingua_CreateSphereBody(
        int worldHandle,
        float px, float py, float pz,
        float qx, float qy, float qz, float qw,
        float radius,
        int bodyType, float density, float friction, float restitution);

    [DllImport(Lib)]
    public static extern void Lingua_DestroyBody(int bodyHandle);

    [DllImport(Lib)]
    public static extern void Lingua_GetBodyTransform(
        int bodyHandle,
        out float px, out float py, out float pz,
        out float qx, out float qy, out float qz, out float qw);

    [DllImport(Lib)]
    public static extern void Lingua_SetBodyTransform(
        int bodyHandle,
        float px, float py, float pz,
        float qx, float qy, float qz, float qw);

    [DllImport(Lib)]
    public static extern void Lingua_ApplyLinearImpulse(
        int bodyHandle, float ix, float iy, float iz, [MarshalAs(UnmanagedType.U1)] bool wake);

    [DllImport(Lib)]
    public static extern void Lingua_GetLinearVelocity(int bodyHandle, out float vx, out float vy, out float vz);

    [DllImport(Lib)]
    public static extern void Lingua_SetLinearVelocity(int bodyHandle, float vx, float vy, float vz);
}
