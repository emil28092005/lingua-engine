using System.Numerics;
using Engine.Editor.Contracts;
using Engine.Kernel.World;
using Engine.Render.Contracts;
using Engine.Windowing.Contracts;
using ImGuiNET;

namespace Engine.Editor;

/// <summary>
/// M3's actual gizmo, per the "полноценный 3D-пайплайн" choice over a
/// scoped 2D one: three axis handles at the selected GameObject's world
/// position, drawn by projecting real 3D points through the real camera's
/// View/Projection (GizmoMath.WorldToScreen) onto ImGui's foreground draw
/// list — not OpenGL geometry, since nothing in engine.render draws lines
/// yet and ImGui's own 2D draw list, fed real 3D-projected coordinates, is
/// already exactly "a handle dragged in screen space mapped onto a real 3D
/// axis." Dragging reads GizmoMath.ProjectDragOntoAxis and writes back
/// through GizmoMath.WorldToLocalPosition — both pure and unit-tested in
/// Engine.Editor.Tests, since neither needs a GL context or a real mouse to
/// verify, only this glue does.
/// </summary>
internal sealed class TranslateGizmo
{
    private const float AxisLength = 1.5f;
    private const float HandleRadius = 6f;

    private static readonly Vector3[] AxisDirections = [Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ];

    private int _dragAxis = -1;
    private Vector2 _dragStartMouse;
    private Vector3 _dragStartWorldPosition;

    public void Draw(EditorState state, ICameraService camera, IEngineWindow window)
    {
        var go = state.Selected;
        if (go is null)
            return;

        var screenSize = new Vector2(window.Native.FramebufferSize.X, window.Native.FramebufferSize.Y);
        if (screenSize.X <= 0 || screenSize.Y <= 0)
            return;

        var viewProjection = camera.View * camera.Projection;
        var worldPos = go.WorldMatrix.Translation;

        var origin2D = GizmoMath.WorldToScreen(worldPos, viewProjection, screenSize);
        if (origin2D is null)
            return;

        var mouse = ImGui.GetIO().MousePos;
        var mouseFree = !ImGui.GetIO().WantCaptureMouse;
        var drawList = ImGui.GetForegroundDrawList();

        for (var axis = 0; axis < 3; axis++)
        {
            var tipWorld = worldPos + AxisDirections[axis] * AxisLength;
            var tip2D = GizmoMath.WorldToScreen(tipWorld, viewProjection, screenSize);
            if (tip2D is null)
                continue;

            var color = AxisColor(axis);
            drawList.AddLine(origin2D.Value, tip2D.Value, color, 3f);
            drawList.AddCircleFilled(tip2D.Value, HandleRadius, color);

            var hovering = Vector2.Distance(mouse, tip2D.Value) <= HandleRadius + 2f;

            if (_dragAxis == -1 && hovering && mouseFree && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
            {
                _dragAxis = axis;
                _dragStartMouse = mouse;
                _dragStartWorldPosition = worldPos;
            }

            if (_dragAxis != axis)
                continue;

            if (!ImGui.IsMouseDown(ImGuiMouseButton.Left))
            {
                _dragAxis = -1;
                continue;
            }

            var delta = GizmoMath.ProjectDragOntoAxis(origin2D.Value, tip2D.Value, _dragStartMouse, mouse, AxisLength);
            var newWorldPos = _dragStartWorldPosition + AxisDirections[axis] * delta;
            var parentMatrix = go.Parent?.WorldMatrix;

            var t = go.Transform;
            t.LocalPosition = GizmoMath.WorldToLocalPosition(newWorldPos, parentMatrix);
            go.Transform = t;
        }
    }

    private static uint AxisColor(int axis) => axis switch
    {
        0 => ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.25f, 0.25f, 1f)), // X: red
        1 => ImGui.ColorConvertFloat4ToU32(new Vector4(0.3f, 1f, 0.3f, 1f)),   // Y: green
        _ => ImGui.ColorConvertFloat4ToU32(new Vector4(0.35f, 0.55f, 1f, 1f)), // Z: blue
    };
}
