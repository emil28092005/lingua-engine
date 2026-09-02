using System.Numerics;

namespace Engine.Editor.Contracts;

/// <summary>
/// The pure math behind a screen-space-drag-to-3D-axis gizmo, split out
/// from TranslateGizmo (Engine.Editor, which owns the ImGui drawing and
/// live mouse/GameObject state) specifically so it's unit-testable without
/// a GL context, a real window, or a mouse — none of which a headless test
/// run has. See TranslateGizmo's own doc comment for how these three
/// functions compose into an actual drag.
/// </summary>
public static class GizmoMath
{
    /// <summary>
    /// Projects a world-space point through view*projection to a
    /// screen-space pixel coordinate. Null when the point is behind the
    /// camera (w &lt;= 0) — there's no sane screen position for it, and
    /// drawing one anyway (naive perspective division) would fling it to
    /// whatever's on the opposite side of the screen instead of just not
    /// being there.
    /// </summary>
    public static Vector2? WorldToScreen(Vector3 worldPos, Matrix4x4 viewProjection, Vector2 screenSize)
    {
        var clip = Vector4.Transform(new Vector4(worldPos, 1f), viewProjection);
        if (clip.W <= 0f)
            return null;

        var ndcX = clip.X / clip.W;
        var ndcY = clip.Y / clip.W;

        // NDC's Y axis points up; screen-space pixel Y points down.
        return new Vector2(
            (ndcX * 0.5f + 0.5f) * screenSize.X,
            (1f - (ndcY * 0.5f + 0.5f)) * screenSize.Y);
    }

    /// <summary>
    /// How far along a world-space axis the current drag corresponds to,
    /// in world units. Works entirely in screen space: how far the mouse
    /// moved along the axis's own on-screen direction (not just its raw XY
    /// delta — an axis pointing diagonally on screen needs the component
    /// of the mouse movement that's actually along it), scaled by how many
    /// world units one screen pixel represented for this axis at drag
    /// start (screenLen pixels spanned axisWorldLength world units).
    ///
    /// This is a projection-ratio approximation, not true ray/nearest-point
    /// axis math — ratio holds exactly only at the handle's own depth, and
    /// drifts slightly as the object moves toward or away from the camera
    /// mid-drag under perspective projection. Good enough for a first
    /// working gizmo; a real nearest-point-on-ray solve is more machinery
    /// than a single translate handle has earned yet.
    /// </summary>
    public static float ProjectDragOntoAxis(
        Vector2 origin2D, Vector2 tip2D, Vector2 dragStartMouse, Vector2 currentMouse, float axisWorldLength)
    {
        var screenDir = tip2D - origin2D;
        var screenLen = screenDir.Length();
        if (screenLen < 0.0001f)
            return 0f;

        var normalizedDir = screenDir / screenLen;
        var mouseDelta = currentMouse - dragStartMouse;
        var pixelsAlongAxis = Vector2.Dot(mouseDelta, normalizedDir);
        return pixelsAlongAxis / screenLen * axisWorldLength;
    }

    /// <summary>
    /// Converts a desired new world-space position into the local position
    /// GameObject.Transform.LocalPosition needs to produce it — the inverse
    /// of GameObject.WorldMatrix's own composition. Null parentWorldMatrix
    /// (no parent) means local and world are the same space. Matters
    /// specifically because a parent's non-identity scale or rotation means
    /// "move 1 world unit along X" and "add 1 to LocalPosition.X" are not
    /// the same thing — see samples/WindowDemo's ChildQuad, parented under
    /// a GameObject with a non-uniform (2,2,1) scale, which is exactly the
    /// case this needs to get right.
    /// </summary>
    public static Vector3 WorldToLocalPosition(Vector3 worldPos, Matrix4x4? parentWorldMatrix)
    {
        if (parentWorldMatrix is null)
            return worldPos;

        Matrix4x4.Invert(parentWorldMatrix.Value, out var inverse);
        return Vector3.Transform(worldPos, inverse);
    }
}
