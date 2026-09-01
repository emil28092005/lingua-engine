using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Engine.Kernel.Events;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

namespace Engine.Kernel.Plugins;

/// <summary>
/// Loads, unloads, and (eventually) reloads plugins. See
/// docs/kernel-contract.md §3-§4.
///
/// Scope of this pass: single-plugin load/unload with the correct two-ALC
/// split, and enough bookkeeping that a plugin's Shutdown() can be honest.
/// NOT yet built: resolving a project's or a plugin's <c>dependsOn</c> graph
/// to determine load order across multiple plugins — that needs at least
/// two real interdependent plugins to test against meaningfully, and we
/// only have one (sandbox.echo, deliberately dependency-free). Loading a
/// plugin whose dependencies aren't already loaded will fail wherever the
/// plugin's own code first touches them, not with a host-level error.
/// </summary>
public sealed class PluginHost(IWorld world, IServiceRegistry services, Schedule schedule, IEventBus events)
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly Dictionary<string, LoadedPlugin> _loaded = [];

    /// <summary>
    /// Loads the plugin described by <c>plugin.json</c> in
    /// <paramref name="pluginDirectory"/>. Contracts load into the Default
    /// ALC; the implementation loads into a fresh collectible ALC. Returns
    /// the plugin's id.
    /// </summary>
    public string Load(string pluginDirectory)
    {
        var manifest = ReadManifest(pluginDirectory);

        if (_loaded.ContainsKey(manifest.Id))
            throw new InvalidOperationException($"Plugin '{manifest.Id}' is already loaded.");

        LoadContractsIntoDefaultAlc(pluginDirectory, manifest);

        var implPath = Path.Combine(pluginDirectory, manifest.Assembly);
        var alc = new PluginLoadContext(manifest.Id, implPath);
        var implAssembly = alc.LoadFromAssemblyPath(implPath);

        var pluginType = FindPluginType(implAssembly, manifest.Id);
        var instance = (IPlugin)Activator.CreateInstance(pluginType)!;

        schedule.RegisterPlugin(manifest.Id, implAssembly);
        var ctx = new PluginContext(manifest.Id, world, services, schedule, events);

        instance.Configure(ctx);

        _loaded[manifest.Id] = new LoadedPlugin(manifest, alc, instance, ctx);
        return manifest.Id;
    }

    /// <summary>
    /// Runs Shutdown(), then unloads the plugin's ALC. Returns a weak
    /// reference to the ALC so a caller can verify it actually collected —
    /// see the leak test this exists for in
    /// Engine.ConformanceHarness/AlcUnloadTests.cs.
    /// </summary>
    public WeakReference Unload(string pluginId)
    {
        if (!_loaded.Remove(pluginId, out var loaded))
            throw new InvalidOperationException($"Plugin '{pluginId}' is not loaded.");

        loaded.Instance.Shutdown(loaded.Context);

        var weakAlc = new WeakReference(loaded.Alc);
        loaded.Alc.Unload();

        return weakAlc;
    }

    private static PluginManifest ReadManifest(string pluginDirectory)
    {
        var path = Path.Combine(pluginDirectory, "plugin.json");

        if (!File.Exists(path))
            throw new FileNotFoundException($"No plugin.json found in '{pluginDirectory}'.", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<PluginManifest>(json, ManifestOptions)
            ?? throw new InvalidOperationException($"'{path}' did not deserialize to a plugin manifest.");
    }

    private static void LoadContractsIntoDefaultAlc(string pluginDirectory, PluginManifest manifest)
    {
        var contractsPath = Path.Combine(pluginDirectory, manifest.Contracts);
        var name = AssemblyName.GetAssemblyName(contractsPath).Name;

        var alreadyLoaded = AssemblyLoadContext.Default.Assemblies
            .Any(a => a.GetName().Name == name);

        if (!alreadyLoaded)
            AssemblyLoadContext.Default.LoadFromAssemblyPath(contractsPath);
    }

    private static Type FindPluginType(Assembly assembly, string pluginId)
    {
        var candidates = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IPlugin).IsAssignableFrom(t))
            .ToList();

        return candidates.Count switch
        {
            0 => throw new InvalidOperationException(
                $"'{assembly.GetName().Name}' (plugin '{pluginId}') has no IPlugin implementation."),
            > 1 => throw new InvalidOperationException(
                $"'{assembly.GetName().Name}' (plugin '{pluginId}') has more than one IPlugin " +
                $"implementation: {string.Join(", ", candidates.Select(t => t.FullName))}."),
            _ => candidates[0],
        };
    }
}
