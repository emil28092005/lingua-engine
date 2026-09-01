namespace Engine.Kernel.Plugins;

/// <summary>
/// A plugin holds no game state — none. State lives in World; the plugin
/// is code that operates on it. See docs/kernel-contract.md §3.
/// </summary>
public interface IPlugin
{
    /// <summary>Registration: services, systems, component types.</summary>
    void Configure(IPluginContext ctx);

    /// <summary>
    /// Full undo of Configure. Whether this method is honest determines
    /// whether the ALC unloads at all — see §4.
    /// </summary>
    void Shutdown(IPluginContext ctx);
}
