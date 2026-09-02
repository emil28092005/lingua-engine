namespace Engine.Kernel.Events;

/// <summary>
/// Decoupled notifications: a publisher fires a fact with no fixed
/// consumer at design time. See the "two channels" — well, three, counting
/// this — table in docs/kernel-contract.md §2.
///
/// Deliberately not "events as World entities": disposable event
/// GameObjects would need something to create and clean them up every
/// frame, which fits a pure-ECS model better than GameObject/Component's
/// persistent-identity one. A real pub/sub bus is the better fit here.
/// </summary>
public interface IEventBus
{
    void Publish<TEvent>(TEvent evt) where TEvent : notnull;

    /// <summary>No matching Unsubscribe — same shape as ISchedule, which
    /// only offers bulk removal by plugin id, not per-system removal.
    /// Call RemoveAllFrom your own plugin id in Shutdown(), or a forgotten
    /// subscription pins your ALC exactly the way a forgotten system
    /// does — see §4.</summary>
    void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : notnull;

    void RemoveAllFrom(string pluginId);
}
