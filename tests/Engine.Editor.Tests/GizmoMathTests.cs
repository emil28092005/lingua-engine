using System.Numerics;
using Engine.Editor.Contracts;

namespace Engine.Editor.Tests;

public class GizmoMathTests
{
    [Fact]
    public void WorldToScreen_OriginProjectsToScreenCenter_ForSymmetricCamera()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, 1f, 0.1f, 100f);
        var screenSize = new Vector2(800, 800);

        var screen = GizmoMath.WorldToScreen(Vector3.Zero, view * projection, screenSize);

        Assert.NotNull(screen);
        Assert.Equal(400f, screen!.Value.X, precision: 3);
        Assert.Equal(400f, screen.Value.Y, precision: 3);
    }

    [Fact]
    public void WorldToScreen_PointBehindCamera_ReturnsNull()
    {
        var view = Matrix4x4.CreateLookAt(new Vector3(0, 0, 5), Vector3.Zero, Vector3.UnitY);
        var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, 1f, 0.1f, 100f);

        // Camera sits at z=5 looking toward z=0; a point at z=10 is on the
        // far side of the camera from where it's looking, not just far away.
        var screen = GizmoMath.WorldToScreen(new Vector3(0, 0, 10), view * projection, new Vector2(800, 800));

        Assert.Null(screen);
    }

    [Fact]
    public void ProjectDragOntoAxis_AlongScreenAxis_ScalesLinearly()
    {
        var origin = new Vector2(100, 100);
        var tip = new Vector2(200, 100); // 100px maps to 2 world units
        var start = new Vector2(100, 100);
        var current = new Vector2(150, 100); // half the drag

        var result = GizmoMath.ProjectDragOntoAxis(origin, tip, start, current, axisWorldLength: 2f);

        Assert.Equal(1f, result, precision: 4);
    }

    [Fact]
    public void ProjectDragOntoAxis_DiagonalAxis_ProjectsOnlyTheAlignedComponent()
    {
        var origin = new Vector2(0, 0);
        var tip = new Vector2(100, 100); // 45-degree axis, length ~141.42
        var start = Vector2.Zero;

        // Move straight along X only — half of it is "along" the diagonal axis.
        var current = new Vector2(100, 0);

        var result = GizmoMath.ProjectDragOntoAxis(origin, tip, start, current, axisWorldLength: 2f);

        Assert.Equal(1f, result, precision: 3);
    }

    [Fact]
    public void ProjectDragOntoAxis_ZeroLengthAxis_ReturnsZero()
    {
        var result = GizmoMath.ProjectDragOntoAxis(
            new Vector2(50, 50), new Vector2(50, 50), Vector2.Zero, new Vector2(999, 999), axisWorldLength: 2f);

        Assert.Equal(0f, result);
    }

    [Fact]
    public void WorldToLocalPosition_NoParent_ReturnsWorldPositionUnchanged()
    {
        var worldPos = new Vector3(3, 4, 5);

        var local = GizmoMath.WorldToLocalPosition(worldPos, parentWorldMatrix: null);

        Assert.Equal(worldPos, local);
    }

    [Fact]
    public void WorldToLocalPosition_ParentWithNonUniformScale_AccountsForScale()
    {
        // Exactly samples/WindowDemo's ChildQuad case: a parent scaled
        // (2,2,1) — moving 2 world units along X must become 1 local unit,
        // not 2, or the object would drift as it's dragged.
        var parentMatrix = Matrix4x4.CreateScale(2, 2, 1);
        var worldPos = new Vector3(2, 0, 0);

        var local = GizmoMath.WorldToLocalPosition(worldPos, parentMatrix);

        Assert.Equal(new Vector3(1, 0, 0), local);
    }

    [Fact]
    public void WorldToLocalPosition_ParentWithTranslation_SubtractsParentOffset()
    {
        var parentMatrix = Matrix4x4.CreateTranslation(5, 0, 0);
        var worldPos = new Vector3(7, 0, 0);

        var local = GizmoMath.WorldToLocalPosition(worldPos, parentMatrix);

        Assert.Equal(new Vector3(2, 0, 0), local);
    }
}
