using Engine.Editor.Contracts;
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
    private PlayModeController? _playMode;

    public void Configure(IPluginContext ctx)
    {
        var window = ctx.Services.Require<IEngineWindow>();
        var input = ctx.Services.Require<IEngineInput>();
        _time = ctx.Time;

        window.Native.GLContext!.MakeCurrent();
        _gl = window.Native.CreateOpenGL();
        _controller = new ImGuiController(_gl, window.Native, input.Native);

        _playMode = new PlayModeController(ctx.World, ctx.Log);
        ctx.Services.Provide<IPlayModeController>(_playMode);

        ctx.Schedule.Add(Stage.Render, DrawUi);
        ctx.Log.Info("editor UI ready (ImGui)");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.editor");
        ctx.Services.Revoke<IPlayModeController>();
        _controller?.Dispose();
        _gl?.Dispose();
        _controller = null;
        _gl = null;
        _time = null;
        _playMode = null;
    }

    private void DrawUi(IWorld world)
    {
        _controller!.Update(_time!.DeltaTime);

        // Nothing selected yet and there's something to select: default to
        // the first root rather than opening on an empty, useless
        // Inspector. Only fires once — any real click overwrites it, and
        // it never fights a deliberate deselect because there's no way to
        // deselect yet.
        if (_state.Selected is null && world.Roots.Count > 0)
            _state.Selected = world.Roots[0];

        // FirstUseEver, not every frame: a real editor session lets the
        // user drag panels wherever they want, and re-forcing a position
        // every frame would fight that the moment they did. This only
        // picks a sane, non-overlapping default before ImGui has ever
        // seen these windows (or after Reset Layout, once that exists).
        ImGui.SetNextWindowPos(new(10, 10), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new(220, 90), ImGuiCond.FirstUseEver);
        ImGui.Begin("Lingua Editor");
        ImGui.Text($"FPS: {1f / MathF.Max(_time.DeltaTime, 0.0001f):F0}");
        ImGui.Text($"Frame: {_time.FrameCount}");
        ImGui.Separator();

        if (ImGui.Button(_playMode!.IsPlaying ? "Stop" : "Play"))
        {
            if (_playMode.IsPlaying)
                _playMode.ExitPlay();
            else
                _playMode.EnterPlay();
        }

        ImGui.SameLine();
        ImGui.Text(_playMode.IsPlaying ? "(Playing)" : "(Edit mode)");
        ImGui.End();

        HierarchyPanel.Draw(world, _state);
        InspectorPanel.Draw(_state);

        _controller.Render();
    }
}
