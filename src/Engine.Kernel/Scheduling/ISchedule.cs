namespace Engine.Kernel.Scheduling;

/// <summary>
/// Frame stages and system ordering. See docs/kernel-contract.md §2.
/// </summary>
public interface ISchedule
{
    ISystemBuilder Add(Stage stage, Delegate system);

    /// <summary>Called from a plugin's Shutdown() — must remove everything
    /// Configure() added, or the ALC it lives in will never unload. See §4.</summary>
    void RemoveAllFrom(string pluginId);
}
