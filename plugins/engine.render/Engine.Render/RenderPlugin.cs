using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;
using Engine.Windowing.Contracts;
using Silk.NET.OpenGL;

namespace Engine.Render;

/// <summary>
/// M1's minimal render pipeline: clears the window to a color and swaps
/// buffers, once per Render stage. No Contracts assembly — nothing here is
/// a type another plugin needs to reference yet (see the null-Contracts
/// note on PluginManifest). See M1 in docs/kernel-contract.md §8.
///
/// This is the whole point of M1's "done when": change ClearColor, rebuild
/// just this plugin, and reload it while the window from engine.windowing
/// stays open — the color changes with no app restart. Verified by hand,
/// not by an automated test — opening a real window needs a real display,
/// which isn't something to assume of every environment this runs in.
///
/// engine.windowing alone produces a window that never becomes visible on
/// Wayland — unlike X11, a Wayland surface with no committed buffer simply
/// isn't shown by the compositor, so an "empty" window isn't even a black
/// rectangle, it's nothing at all. This plugin's first Clear+SwapBuffers is
/// what actually makes the window appear.
/// </summary>
public sealed class RenderPlugin : IPlugin
{
    private static readonly float[] ClearColor = [0.25f, 0.55f, 0.85f, 1f];

    private GL? _gl;
    private IEngineWindow? _window;

    public void Configure(IPluginContext ctx)
    {
        _window = ctx.Services.Require<IEngineWindow>();
        _window.Native.GLContext!.MakeCurrent();
        _gl = _window.Native.CreateOpenGL();

        ctx.Schedule.Add(Stage.Render, Draw);
        ctx.Log.Info("GL context created");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.render");
        _gl?.Dispose();
        _gl = null;
        _window = null;
    }

    private void Draw(IWorld world)
    {
        _gl!.ClearColor(ClearColor[0], ClearColor[1], ClearColor[2], ClearColor[3]);
        _gl.Clear(ClearBufferMask.ColorBufferBit);
        _window!.Native.GLContext!.SwapBuffers();
    }
}
