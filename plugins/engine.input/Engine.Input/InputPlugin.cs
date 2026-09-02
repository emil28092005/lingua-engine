using Engine.Kernel.Plugins;
using Engine.Input.Contracts;
using Engine.Windowing.Contracts;
using Silk.NET.Input;

namespace Engine.Input;

/// <summary>
/// M1's third plugin. Publishes keyboard state as a service — nothing in
/// the engine reacts to it yet, since there's no gameplay code to react
/// with; the bar this clears is the same one engine.windowing cleared
/// before engine.render existed to prove it visually: constructs cleanly
/// against a real window, doesn't throw. See M1 in docs/kernel-contract.md
/// §8.
/// </summary>
public sealed class InputPlugin : IPlugin
{
    private IInputContext? _input;

    public void Configure(IPluginContext ctx)
    {
        var window = ctx.Services.Require<IEngineWindow>();
        _input = window.Native.CreateInput();

        ctx.Services.Provide<IEngineInput>(new SilkEngineInput(_input));
        ctx.Log.Info($"input ready ({_input.Keyboards.Count} keyboard(s), {_input.Mice.Count} mouse(s))");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Services.Revoke<IEngineInput>();
        _input?.Dispose();
        _input = null;
    }
}
