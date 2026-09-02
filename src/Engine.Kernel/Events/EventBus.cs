using System.Reflection;

namespace Engine.Kernel.Events;

/// <summary>
/// Ownership tracked the same way Schedule tracks systems: by the
/// subscribing delegate's declaring assembly, not a string tag per
/// subscription. See <see cref="RegisterPlugin"/> and
/// Engine.Kernel.Scheduling.Schedule.
/// </summary>
public sealed class EventBus : IEventBus
{
    private readonly Dictionary<Type, List<Delegate>> _handlers = [];
    private readonly Dictionary<string, Assembly> _pluginAssemblies = [];

    /// <summary>Called by PluginHost right after loading a plugin's
    /// implementation assembly, before Configure() runs — mirrors
    /// Schedule.RegisterPlugin exactly.</summary>
    internal void RegisterPlugin(string pluginId, Assembly implementationAssembly)
        => _pluginAssemblies[pluginId] = implementationAssembly;

    public void Publish<TEvent>(TEvent evt) where TEvent : notnull
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers) || handlers.Count == 0)
            return;

        // Snapshot: a handler subscribing or unsubscribing during dispatch
        // must not corrupt the in-progress iteration.
        foreach (var handler in handlers.ToArray())
            ((Action<TEvent>)handler)(evt);
    }

    public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull
    {
        if (!_handlers.TryGetValue(typeof(TEvent), out var handlers))
        {
            handlers = [];
            _handlers[typeof(TEvent)] = handlers;
        }

        handlers.Add(handler);
    }

    public void RemoveAllFrom(string pluginId)
    {
        if (!_pluginAssemblies.Remove(pluginId, out var assembly))
            return;

        foreach (var handlers in _handlers.Values)
            handlers.RemoveAll(h => h.Method.DeclaringType?.Assembly == assembly);
    }
}
