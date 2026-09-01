namespace Engine.Kernel.Plugins;

/// <summary>
/// Deserialized shape of a project's <c>project.json</c> — which plugins a
/// specific game loads, at which versions, and where to find its own. See
/// "Per-project configuration" in docs/kernel-contract.md §2.
/// </summary>
public sealed class ProjectManifest
{
    public required string EngineVersion { get; init; }

    public List<PluginReference> Plugins { get; init; } = new();

    /// <summary>Search paths for plugins local to this project, not shipped
    /// with the engine.</summary>
    public List<string> PluginPaths { get; init; } = new();
}

public sealed class PluginReference
{
    public required string Id { get; init; }

    /// <summary>Null means "whatever the engine ships by default."</summary>
    public string? Version { get; init; }
}
