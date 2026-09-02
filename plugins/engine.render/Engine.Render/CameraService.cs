using System.Numerics;
using Engine.Render.Contracts;
using Engine.Windowing.Contracts;

namespace Engine.Render;

internal sealed class CameraService(IEngineWindow window) : ICameraService
{
    public Vector3 Position { get; set; } = new(0f, 3f, 6f);
    public Vector3 Target { get; set; } = Vector3.Zero;

    public Matrix4x4 View => Matrix4x4.CreateLookAt(Position, Target, Vector3.UnitY);

    public Matrix4x4 Projection
    {
        get
        {
            var size = window.Native.FramebufferSize;
            var aspect = size.Y == 0 ? 1f : (float)size.X / size.Y;
            return Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, aspect, 0.1f, 100f);
        }
    }
}
