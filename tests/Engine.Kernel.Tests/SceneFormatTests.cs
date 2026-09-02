using System.Numerics;
using System.Text.Json;
using Engine.Kernel.World;

namespace Engine.Kernel.Tests;

public class SceneFormatTests
{
    private sealed class Health : Component
    {
        public int Value;
    }

    [Fact]
    public void ToJson_Includes_Name_Transform_And_Component_Fields()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("Hero");
        go.Transform.LocalPosition = new Vector3(1, 2, 3);
        go.AddComponent<Health>().Value = 42;

        using var doc = JsonDocument.Parse(SceneFormat.ToJson(world));
        var root = doc.RootElement[0];

        Assert.Equal("Hero", root.GetProperty("name").GetString());

        var position = root.GetProperty("transform").GetProperty("position");
        Assert.Equal(1, position[0].GetSingle());
        Assert.Equal(2, position[1].GetSingle());
        Assert.Equal(3, position[2].GetSingle());

        var component = root.GetProperty("components")[0];
        Assert.Contains("Health", component.GetProperty("type").GetString());
        Assert.Equal(42, component.GetProperty("data").GetProperty("Value").GetInt32());
    }

    [Fact]
    public void ToJson_Nests_Children_Under_Their_Parent_Rather_Than_Listing_Them_At_The_Top_Level()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");
        child.SetParent(parent);

        using var doc = JsonDocument.Parse(SceneFormat.ToJson(world));

        Assert.Equal(1, doc.RootElement.GetArrayLength());
        var root = doc.RootElement[0];
        Assert.Equal("Parent", root.GetProperty("name").GetString());
        Assert.Equal("Child", root.GetProperty("children")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void ToJson_Represents_Duplicate_Components_Of_The_Same_Type_As_A_List_Not_A_Dictionary()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("A");
        go.AddComponent<Health>().Value = 1;
        go.AddComponent<Health>().Value = 2;

        // A dictionary keyed by type name would throw on this exact case.
        using var doc = JsonDocument.Parse(SceneFormat.ToJson(world));

        var components = doc.RootElement[0].GetProperty("components");
        Assert.Equal(2, components.GetArrayLength());
    }

    [Fact]
    public void Round_Trip_Recreates_Name_Transform_And_Component_Field_Values()
    {
        var source = new GameWorld();
        var go = source.CreateGameObject("Hero");
        go.Transform.LocalPosition = new Vector3(1, 2, 3);
        go.Transform.LocalScale = new Vector3(2, 2, 2);
        go.AddComponent<Health>().Value = 42;

        var json = SceneFormat.ToJson(source);

        var loaded = new GameWorld();
        SceneFormat.FromJson(loaded, json);

        var root = Assert.Single(loaded.Roots);
        Assert.Equal("Hero", root.Name);
        Assert.Equal(new Vector3(1, 2, 3), root.Transform.LocalPosition);
        Assert.Equal(new Vector3(2, 2, 2), root.Transform.LocalScale);
        Assert.Equal(42, root.GetComponent<Health>()!.Value);
    }

    [Fact]
    public void Round_Trip_Recreates_The_Parent_Child_Hierarchy()
    {
        var source = new GameWorld();
        var parent = source.CreateGameObject("Parent");
        var child = source.CreateGameObject("Child");
        child.SetParent(parent);

        var json = SceneFormat.ToJson(source);

        var loaded = new GameWorld();
        SceneFormat.FromJson(loaded, json);

        var root = Assert.Single(loaded.Roots);
        Assert.Equal("Parent", root.Name);
        var loadedChild = Assert.Single(root.Children);
        Assert.Equal("Child", loadedChild.Name);
        Assert.Same(root, loadedChild.Parent);
    }

    [Fact]
    public void Load_Is_Additive_Onto_An_Already_Populated_World()
    {
        var source = new GameWorld();
        source.CreateGameObject("FromScene");

        var json = SceneFormat.ToJson(source);

        var target = new GameWorld();
        target.CreateGameObject("AlreadyThere");
        SceneFormat.FromJson(target, json);

        Assert.Equal(2, target.Roots.Count);
        Assert.Contains(target.Roots, go => go.Name == "AlreadyThere");
        Assert.Contains(target.Roots, go => go.Name == "FromScene");
    }

    [Fact]
    public void Load_Throws_A_Clear_Error_When_A_Component_Type_Is_Not_Loaded()
    {
        const string json = """
            [
              {
                "name": "Broken",
                "transform": { "position": [0,0,0], "rotation": [0,0,0,1], "scale": [1,1,1] },
                "components": [ { "type": "Nonexistent.Ghost, Nonexistent", "data": {} } ],
                "children": []
              }
            ]
            """;

        var world = new GameWorld();

        var exception = Assert.Throws<InvalidOperationException>(() => SceneFormat.FromJson(world, json));
        Assert.Contains("Nonexistent.Ghost", exception.Message);
    }
}
