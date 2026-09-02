using Engine.Assets.Contracts;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;

namespace Engine.Assets;

/// <summary>
/// M2's second half: engine.windowing made a window visible, engine.render
/// made it draw something, this makes what it draws hot-reloadable from a
/// file on disk — the actual "done when" for M2. See docs/kernel-contract.md
/// §8.
/// </summary>
public sealed class AssetPlugin : IPlugin
{
    private AssetService? _service;

    public void Configure(IPluginContext ctx)
    {
        _service = new AssetService(ctx.Events, ctx.Log);
        ctx.Services.Provide<IAssetService>(_service);

        // Pumping on Update, not from the FileSystemWatcher callback
        // directly — see the note on AssetService.PumpReloads.
        ctx.Schedule.Add(Stage.Update, _ => _service!.PumpReloads());
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.assets");
        ctx.Services.Revoke<IAssetService>();
        _service?.Dispose();
        _service = null;
    }
}
