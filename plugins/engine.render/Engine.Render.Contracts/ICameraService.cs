using System.Numerics;

namespace Engine.Render.Contracts;

/// <summary>
/// The one camera engine.render draws through — read for gizmo/ray-picking
/// math (editor.shell needs View/Projection to turn a 2D mouse position
/// into a 3D ray), settable for camera controls (orbit, fly) that don't
/// exist yet. Not a Component/GameObject-driven multi-camera system —
/// there's exactly one camera and nothing needs more than that yet; see
/// the note on RenderPlugin for why that's a deliberate scope line, not
/// an oversight.
/// </summary>
public interface ICameraService
{
    Vector3 Position { get; set; }
    Vector3 Target { get; set; }

    Matrix4x4 View { get; }

    /// <summary>Recomputed against the window's current size on every
    /// read — a cached value would go stale the moment the window
    /// resizes.</summary>
    Matrix4x4 Projection { get; }
}
