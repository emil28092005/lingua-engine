using Engine.Kernel.Plugins;
using Engine.Windowing.Contracts;
using Silk.NET.Windowing;

namespace Engine.Windowing;

/// <summary>
/// M1's first real plugin: opens an actual window and publishes it as a
/// service. Doesn't drive the frame loop itself — Engine.Host does that,
/// pumping window events plus Schedule.RunStage each frame, same as it
/// already does for the headless case. See M1 in docs/kernel-contract.md §8.
/// </summary>
public sealed class WindowingPlugin : IPlugin
{
    private IWindow? _window;

    public void Configure(IPluginContext ctx)
    {
        var options = WindowOptions.Default with
        {
            Size = new(1280, 720),
            Title = "Lingua Engine",
        };

        _window = Window.Create(options);
        _window.Initialize();

        ctx.Services.Provide<IEngineWindow>(new SilkEngineWindow(_window));
        ctx.Log.Info($"window created ({options.Size.X}x{options.Size.Y})");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Services.Revoke<IEngineWindow>();
        _window?.Dispose();
        _window = null;
    }
}
