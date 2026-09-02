using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.Events;
using Engine.Kernel.Scheduling;
using Engine.Kernel.Services;
using Engine.Kernel.World;

namespace Engine.Kernel.Plugins;

/// <summary>
/// Loads, unloads, and (eventually) reloads plugins. See
/// docs/kernel-contract.md §3-§4.
///
/// NOT yet built: resolving a plugin's own <c>dependsOn</c> graph to
/// determine load order — that needs at least two real interdependent
/// plugins to test against meaningfully, and we only have one (sandbox.echo,
/// deliberately dependency-free). <see cref="LoadProject"/> loads a
/// project's plugins in the order its manifest lists them and does not
/// check <c>PluginReference.Version</c> either; loading a plugin whose
/// dependencies aren't already loaded (or aren't the version expected)
/// will fail wherever its own code first touches them, not with a
/// host-level error.
/// </summary>
public sealed class PluginHost(
    IWorld world,
    IServiceRegistry services,
    Schedule schedule,
    EventBus events,
    ITime time)
{
    private static readonly JsonSerializerOptions ManifestOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Shared across every PluginHost in the process, not per-instance:
    // AssemblyLoadContext.Default.Resolving is itself process-wide, and an
    // instance-bound handler on it would keep that PluginHost reachable
    // forever — exactly the kind of leak this whole architecture exists to
    // avoid, just aimed at a host instead of a plugin ALC. See
    // EnsureDefaultResolvingHooked.
    private static readonly List<AssemblyDependencyResolver> ContractResolvers = [];
    private static readonly Lock ContractResolversLock = new();
    private static bool _defaultResolvingHooked;

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
        events.RegisterPlugin(manifest.Id, implAssembly);
        var ctx = new PluginContext(manifest.Id, world, services, schedule, events, time);

        try
        {
            instance.Configure(ctx);
        }
        catch
        {
            // Configure can fail after already doing real work — Schedule.
            // Add, Events.Subscribe, Services.Provide calls all happen
            // before whatever line actually throws. None of that unwinds
            // on its own, and because _loaded never gets an entry for this
            // id below, the plugin ends up neither loaded nor unloadable:
            // its ALC stays rooted forever and any systems/subscriptions it
            // did register before throwing keep running. Best-effort
            // Shutdown first — it's the only thing that knows which
            // services this specific plugin provided, so it's the only way
            // to Revoke them — then the two RemoveAllFrom calls PluginHost
            // itself can make unconditionally, then unload the ALC. Each
            // step is wrapped so a broken Shutdown/unload can't hide the
            // real Configure failure being rethrown below.
            try { instance.Shutdown(ctx); } catch { /* best-effort */ }

            schedule.RemoveAllFrom(manifest.Id);
            events.RemoveAllFrom(manifest.Id);

            try { alc.Unload(); } catch { /* best-effort */ }

            throw;
        }

        _loaded[manifest.Id] = new LoadedPlugin(manifest, alc, instance, ctx);
        events.Publish(new PluginLoaded(manifest.Id));
        return manifest.Id;
    }

    /// <summary>
    /// Loads every plugin a project's manifest lists, in listed order.
    /// Each plugin id is resolved to a directory by checking, in order,
    /// every path in <paramref name="engineSearchPaths"/> (the engine's own
    /// plugin catalog) and then the project's own <c>pluginPaths</c>
    /// (resolved relative to <paramref name="projectManifestPath"/>'s
    /// directory) for a <c>&lt;searchPath&gt;/&lt;id&gt;/plugin.json</c>.
    /// Returns the loaded ids, same order as the manifest.
    /// </summary>
    public IReadOnlyList<string> LoadProject(string projectManifestPath, IReadOnlyList<string> engineSearchPaths)
    {
        var project = ReadProjectManifest(projectManifestPath);
        var projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectManifestPath))!;

        var searchPaths = engineSearchPaths
            .Concat(project.PluginPaths.Select(p => Path.Combine(projectDirectory, p)))
            .ToList();

        var loadedIds = new List<string>(project.Plugins.Count);

        foreach (var reference in project.Plugins)
        {
            var directory = ResolvePluginDirectory(reference.Id, searchPaths)
                ?? throw new InvalidOperationException(
                    $"Could not find plugin '{reference.Id}' under any of: {string.Join(", ", searchPaths)}.");

            loadedIds.Add(Load(directory));
        }

        return loadedIds;
    }

    /// <summary>
    /// Lets a caller check before calling Unload/Load instead of catching
    /// InvalidOperationException to find out — Engine.Host's live-reload
    /// command needs this: a plugin whose previous reload attempt failed
    /// partway through Load isn't loaded any more (see Load's own rollback
    /// on a Configure failure), so unconditionally trying Unload first
    /// would itself throw "not loaded" on every retry.
    /// </summary>
    public bool IsLoaded(string pluginId) => _loaded.ContainsKey(pluginId);

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

        events.Publish(new PluginUnloaded(pluginId));
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

    private static ProjectManifest ReadProjectManifest(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException($"No project manifest found at '{path}'.", path);

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProjectManifest>(json, ManifestOptions)
            ?? throw new InvalidOperationException($"'{path}' did not deserialize to a project manifest.");
    }

    private static string? ResolvePluginDirectory(string pluginId, IReadOnlyList<string> searchPaths)
    {
        foreach (var searchPath in searchPaths)
        {
            var candidate = Path.Combine(searchPath, pluginId);
            if (File.Exists(Path.Combine(candidate, "plugin.json")))
                return candidate;
        }

        return null;
    }

    /// <summary>
    /// Loading a Contracts assembly straight into the Default ALC says
    /// nothing about how ITS OWN dependencies (beyond Engine.Kernel) get
    /// resolved — unlike a plugin's implementation, which always gets a
    /// PluginLoadContext with a real AssemblyDependencyResolver behind it.
    /// This went unnoticed for a while: engine.windowing's and
    /// engine.render's Contracts both depend on Silk.NET packages, but
    /// Engine.Host happens to reference those same Contracts projects
    /// directly (to drive the windowed loop and screenshot capture — see
    /// Program.cs), so their transitive dependencies were already sitting
    /// in Engine.Host's own output directory and got found by luck via
    /// normal probing. engine.input's Contracts has no such lucky
    /// coincidence: Engine.Host has no reason to reference it, so its
    /// Silk.NET.Input dependency wasn't anywhere the default resolution
    /// order would look — a real FileNotFoundException, not a hypothetical
    /// one. Hooking Default.Resolving with a resolver built against each
    /// loaded Contracts path fixes it for real, rather than for whichever
    /// Contracts assemblies happen to also be referenced by whatever's
    /// hosting the engine this time.
    /// </summary>
    private static void LoadContractsIntoDefaultAlc(string pluginDirectory, PluginManifest manifest)
    {
        if (manifest.Contracts is null)
            return;

        var contractsPath = Path.Combine(pluginDirectory, manifest.Contracts);
        var name = AssemblyName.GetAssemblyName(contractsPath).Name;

        var alreadyLoaded = AssemblyLoadContext.Default.Assemblies
            .Any(a => a.GetName().Name == name);

        if (alreadyLoaded)
            return;

        EnsureDefaultResolvingHooked();

        lock (ContractResolversLock)
            ContractResolvers.Add(new AssemblyDependencyResolver(contractsPath));

        AssemblyLoadContext.Default.LoadFromAssemblyPath(contractsPath);
    }

    private static void EnsureDefaultResolvingHooked()
    {
        if (_defaultResolvingHooked)
            return;

        _defaultResolvingHooked = true;

        AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            lock (ContractResolversLock)
            {
                foreach (var resolver in ContractResolvers)
                {
                    var path = resolver.ResolveAssemblyToPath(name);
                    if (path is not null)
                        return AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
                }
            }

            return null;
        };
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
