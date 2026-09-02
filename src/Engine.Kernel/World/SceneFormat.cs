using System.Numerics;
using System.Text.Json;

namespace Engine.Kernel.World;

/// <summary>
/// Save and load a World. Doubles as the headless introspection dump from
/// docs/kernel-contract.md §7 (previously a separate WorldDumper) — there
/// was never a real reason for "what an agent reads to check a frame" and
/// "what a scene file actually is" to be two different JSON shapes, and
/// keeping them one removes the question of which one a save/load round
/// trip is supposed to match. Read/write both happen outside any system's
/// execution, so neither is subject to SystemAccessScope enforcement.
///
/// Component types are arbitrary plugin-defined classes, so this leans on
/// System.Text.Json's own reflection rather than anything bespoke —
/// including IncludeFields, since components are plain public fields
/// (docs/kernel-contract.md §1), not properties. A component is tagged
/// with "TypeFullName, AssemblyName" (partial-name form, no version) —
/// exact enough for Type.GetType to resolve it against whatever's loaded,
/// loose enough that a plugin's incidental version bump doesn't strand
/// every scene that references it.
/// </summary>
public static class SceneFormat
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        IncludeFields = true,
    };

    public static void Save(IWorld world, string path) =>
        File.WriteAllText(path, ToJson(world));

    public static string ToJson(IWorld world)
    {
        var roots = world.Roots.Select(Dump).ToList();
        return JsonSerializer.Serialize(roots, Options);
    }

    /// <summary>
    /// Additive: creates whatever this file describes in <paramref
    /// name="world"/> without touching what's already there. A "replace
    /// everything" load is the caller's call to make (Destroy the existing
    /// roots first) — additive is the more fundamental operation, and nothing
    /// today needs the other one.
    /// </summary>
    public static void Load(IWorld world, string path) =>
        FromJson(world, File.ReadAllText(path));

    public static void FromJson(IWorld world, string json)
    {
        using var doc = JsonDocument.Parse(json);

        foreach (var element in doc.RootElement.EnumerateArray())
            Build(world, element, parent: null);
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
            .Select(c => new { type = TypeTag(c.GetType()), data = (object)c })
            .ToList(),
        children = go.Children.Select(Dump).ToList(),
    };

    private static void Build(IWorld world, JsonElement element, GameObject? parent)
    {
        var go = world.CreateGameObject(element.GetProperty("name").GetString()!);

        var transform = element.GetProperty("transform");
        var position = transform.GetProperty("position");
        var rotation = transform.GetProperty("rotation");
        var scale = transform.GetProperty("scale");

        go.Transform = new Transform
        {
            LocalPosition = new Vector3(
                position[0].GetSingle(), position[1].GetSingle(), position[2].GetSingle()),
            LocalRotation = new Quaternion(
                rotation[0].GetSingle(), rotation[1].GetSingle(),
                rotation[2].GetSingle(), rotation[3].GetSingle()),
            LocalScale = new Vector3(scale[0].GetSingle(), scale[1].GetSingle(), scale[2].GetSingle()),
        };

        foreach (var componentElement in element.GetProperty("components").EnumerateArray())
        {
            var typeTag = componentElement.GetProperty("type").GetString()!;
            var componentType = Type.GetType(typeTag)
                ?? throw new InvalidOperationException(
                    $"Scene references component type '{typeTag}', which isn't loaded. " +
                    "Load the plugin that provides it before loading this scene.");

            var component = (Component)JsonSerializer.Deserialize(
                componentElement.GetProperty("data").GetRawText(), componentType, Options)!;

            go.AddComponent(component);
        }

        if (parent is not null)
            go.SetParent(parent);

        foreach (var childElement in element.GetProperty("children").EnumerateArray())
            Build(world, childElement, go);
    }

    private static string TypeTag(Type type) => $"{type.FullName}, {type.Assembly.GetName().Name}";

    private static float[] ToArray(Vector3 v) => [v.X, v.Y, v.Z];
}
