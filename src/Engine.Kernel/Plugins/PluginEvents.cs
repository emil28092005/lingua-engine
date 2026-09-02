namespace Engine.Kernel.Plugins;

/// <summary>
/// The concrete proof that EventBus is real infrastructure, not an
/// unused API — PluginHost publishes both. See docs/kernel-contract.md §2.
/// </summary>
public readonly record struct PluginLoaded(string PluginId);

public readonly record struct PluginUnloaded(string PluginId);
