namespace Engine.Kernel.Diagnostics;

/// <summary>Prefixes every line with the owning plugin's id — one instance
/// per <see cref="Plugins.PluginContext"/>, not shared.</summary>
internal sealed class ConsoleLogger(string pluginId) : ILogger
{
    public void Info(string message) => Console.WriteLine($"[{pluginId}] {message}");
    public void Warn(string message) => Console.WriteLine($"[{pluginId}] WARN: {message}");
    public void Error(string message) => Console.Error.WriteLine($"[{pluginId}] ERROR: {message}");
}
