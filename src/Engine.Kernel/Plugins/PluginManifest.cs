namespace Engine.Kernel.Plugins;

/// <summary>
/// Deserialized shape of a plugin's <c>plugin.json</c>. Read *before*
/// anything loads, so the Plugin Host can build the dependency graph ahead
/// of any ALC loading — see docs/kernel-contract.md §3.
/// </summary>
public sealed class PluginManifest
{
    public required string Id { get; init; }
    public required string Version { get; init; }

    /// <summary>Assembly loaded into the Default ALC. Never unloads. See §4.
    /// Null for a plugin with no types or interfaces for anything else to
    /// reference — manufacturing an empty Contracts assembly just to fill
    /// this field would document nothing real.</summary>
    public string? Contracts { get; init; }

    /// <summary>Assembly loaded into a collectible ALC. Reloadable. See §4.</summary>
    public required string Assembly { get; init; }

    public Dictionary<string, string> DependsOn { get; init; } = new();

    public bool Reloadable { get; init; } = true;
}
