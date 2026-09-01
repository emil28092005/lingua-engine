using System.Text.Json;
using Engine.Kernel.World;

namespace Engine.Kernel.Diagnostics;

/// <summary>
/// Serializes a World to JSON for the headless introspection loop described
/// in docs/kernel-contract.md §7 — an agent's only way to see the effect of
/// an edit without a screen. Read-only and outside any system's execution,
/// so it isn't subject to SystemAccessScope enforcement.
///
/// Component types are arbitrary plugin-defined classes, so this leans on
/// System.Text.Json's own reflection rather than anything bespoke —
/// including IncludeFields, since components are plain public fields
/// (docs/kernel-contract.md §1), not properties.
/// </summary>
public static class WorldDumper
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public static string ToJson(IWorld world)
    {
        var roots = world.Roots.Select(Dump).ToList();
        return JsonSerializer.Serialize(roots, Options);
    }

    private static object Dump(GameObject go) => new
    {
        name = go.Name,
        transform = new
        {
            position = ToArray(go.Transform.LocalPosition),
            rotation = new[]
            {
                go.Transform.LocalRotation.X, go.Transform.LocalRotation.Y,
                go.Transform.LocalRotation.Z, go.Transform.LocalRotation.W,
            },
            scale = ToArray(go.Transform.LocalScale),
        },
        // A list, not a dictionary keyed by type name: AddComponent<T>()
        // doesn't enforce uniqueness (see the World row in §2), so a
        // GameObject can legitimately carry two components of the same
        // type. A dictionary would throw on the very case this needs to
        // represent correctly.
        components = go.Components
            .Select(c => new { type = c.GetType().Name, data = (object)c })
            .ToList(),
        children = go.Children.Select(Dump).ToList(),
    };

    private static float[] ToArray(System.Numerics.Vector3 v) => [v.X, v.Y, v.Z];
}
