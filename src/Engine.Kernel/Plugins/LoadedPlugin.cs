namespace Engine.Kernel.Plugins;

internal sealed record LoadedPlugin(
    PluginManifest Manifest,
    PluginLoadContext Alc,
    IPlugin Instance,
    IPluginContext Context);
