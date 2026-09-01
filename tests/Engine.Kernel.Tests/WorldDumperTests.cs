using System.Numerics;
using System.Text.Json;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.World;

namespace Engine.Kernel.Tests;

public class WorldDumperTests
{
    private sealed class Health : Component
    {
        public int Value;
    }

    [Fact]
    public void Dump_Includes_Name_Transform_And_Component_Fields()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("Hero");
        go.Transform.LocalPosition = new Vector3(1, 2, 3);
        go.AddComponent<Health>().Value = 42;

        using var doc = JsonDocument.Parse(WorldDumper.ToJson(world));
        var root = doc.RootElement[0];

        Assert.Equal("Hero", root.GetProperty("name").GetString());

        var position = root.GetProperty("transform").GetProperty("position");
        Assert.Equal(1, position[0].GetSingle());
        Assert.Equal(2, position[1].GetSingle());
        Assert.Equal(3, position[2].GetSingle());

        var component = root.GetProperty("components")[0];
        Assert.Equal("Health", component.GetProperty("type").GetString());
        Assert.Equal(42, component.GetProperty("data").GetProperty("Value").GetInt32());
    }

    [Fact]
    public void Dump_Nests_Children_Under_Their_Parent_Rather_Than_Listing_Them_At_The_Top_Level()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");
        child.SetParent(parent);

        using var doc = JsonDocument.Parse(WorldDumper.ToJson(world));

        Assert.Equal(1, doc.RootElement.GetArrayLength());
        var root = doc.RootElement[0];
        Assert.Equal("Parent", root.GetProperty("name").GetString());
        Assert.Equal("Child", root.GetProperty("children")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Dump_Represents_Duplicate_Components_Of_The_Same_Type_As_A_List_Not_A_Dictionary()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("A");
        go.AddComponent<Health>().Value = 1;
        go.AddComponent<Health>().Value = 2;

        // A dictionary keyed by type name would throw on this exact case.
        using var doc = JsonDocument.Parse(WorldDumper.ToJson(world));

        var components = doc.RootElement[0].GetProperty("components");
        Assert.Equal(2, components.GetArrayLength());
    }
}
