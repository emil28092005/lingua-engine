using System.Reflection;
using System.Runtime.Loader;

namespace Engine.Kernel.Plugins;

/// <summary>
/// The collectible ALC a plugin's implementation assembly loads into. See
/// docs/kernel-contract.md §4.
///
/// The one thing this has to get right: <see cref="Load"/> must defer to
/// whatever's already sitting in the Default ALC — Engine.Kernel, and this
/// plugin's own Contracts assembly, both loaded there before this context
/// exists — rather than loading a second copy of either. A second copy
/// would carry its own distinct runtime <c>Type</c> objects, and every
/// <c>is T</c> / <c>GetComponent&lt;T&gt;()</c> check across the plugin
/// boundary would silently fail. Only a genuinely private dependency of
/// this specific plugin should ever load through this context.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginId, string mainAssemblyPath)
        : base(name: $"plugin:{pluginId}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        var alreadyInDefault = Default.Assemblies
            .Any(a => a.GetName().Name == assemblyName.Name);

        if (alreadyInDefault)
            return null; // defer — the runtime resolves this to the Default copy

        var path = _resolver.ResolveAssemblyToPath(assemblyName);
        return path is not null ? LoadFromAssemblyPath(path) : null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        var path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is not null ? LoadUnmanagedDllFromPath(path) : IntPtr.Zero;
    }
}
