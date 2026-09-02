using Engine.Input.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Engine.Windowing.Contracts;
using ImGuiNET;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;

namespace Engine.Editor;

/// <summary>
/// M3's editor shell: an ImGui overlay drawn on top of the running scene.
/// Registers on Stage.Render, after engine.render's own Draw (per
/// project.json's plugin order) so it paints into the same back buffer the
/// scene just drew into — and before Stage.Present's SwapBuffers, so the
/// UI isn't delayed a frame. See Stage.Present's doc comment for why that
/// split exists at all.
///
/// Nothing here reads or writes a Component through GetComponent/Query —
/// HierarchyPanel walks IWorld.Roots/GameObject.Children directly, and the
/// not-yet-built Inspector will walk GameObject.Components the same way —
/// which is why this plugin never needs to declare Reads/Writes on its
/// system: those checks only guard the typed accessors, not plain property
/// reads. See GameObject.AddComponent's own doc comment on the same gap.
/// </summary>
public sealed class EditorPlugin : IPlugin
{
    private readonly EditorState _state = new();
    private GL? _gl;
    private ImGuiController? _controller;
    private ITime? _time;

    public void Configure(IPluginContext ctx)
    {
        var window = ctx.Services.Require<IEngineWindow>();
        var input = ctx.Services.Require<IEngineInput>();
        _time = ctx.Time;

        window.Native.GLContext!.MakeCurrent();
        _gl = window.Native.CreateOpenGL();
        _controller = new ImGuiController(_gl, window.Native, input.Native);

        ctx.Schedule.Add(Stage.Render, DrawUi);
        ctx.Log.Info("editor UI ready (ImGui)");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.editor");
        _controller?.Dispose();
        _gl?.Dispose();
        _controller = null;
        _gl = null;
        _time = null;
    }

    private void DrawUi(IWorld world)
    {
        _controller!.Update(_time!.DeltaTime);

        ImGui.Begin("Lingua Editor");
        ImGui.Text($"FPS: {1f / MathF.Max(_time.DeltaTime, 0.0001f):F0}");
        ImGui.Text($"Frame: {_time.FrameCount}");
        ImGui.End();

        HierarchyPanel.Draw(world, _state);

        _controller.Render();
    }
}
