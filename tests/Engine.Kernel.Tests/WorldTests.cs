using System.Numerics;
using Engine.Kernel.World;

namespace Engine.Kernel.Tests;

public class WorldTests
{
    [Fact]
    public void CreateGameObject_Is_A_Root_With_Identity_Transform()
    {
        var world = new GameWorld();

        var go = world.CreateGameObject("Player");

        Assert.Equal("Player", go.Name);
        Assert.Null(go.Parent);
        Assert.Contains(go, world.Roots);
        Assert.Equal(Vector3.Zero, go.Transform.LocalPosition);
        Assert.Equal(Quaternion.Identity, go.Transform.LocalRotation);
        Assert.Equal(Vector3.One, go.Transform.LocalScale);
    }

    [Fact]
    public void SetParent_Moves_GameObject_Out_Of_Roots_And_Into_Children()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");

        child.SetParent(parent);

        Assert.Same(parent, child.Parent);
        Assert.Contains(child, parent.Children);
        Assert.DoesNotContain(child, world.Roots);
        Assert.Contains(parent, world.Roots);
    }

    [Fact]
    public void SetParent_Null_Returns_GameObject_To_Roots()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");
        child.SetParent(parent);

        child.SetParent(null);

        Assert.Null(child.Parent);
        Assert.DoesNotContain(child, parent.Children);
        Assert.Contains(child, world.Roots);
    }

    [Fact]
    public void SetParent_Rejects_Self_Parenting()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("Solo");

        Assert.Throws<InvalidOperationException>(() => go.SetParent(go));
    }

    [Fact]
    public void SetParent_Rejects_A_Cycle_Through_A_Descendant()
    {
        var world = new GameWorld();
        var grandparent = world.CreateGameObject("Grandparent");
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");
        parent.SetParent(grandparent);
        child.SetParent(parent);

        // grandparent is child's own descendant-of-a-descendant here —
        // reparenting it under child would close the loop.
        Assert.Throws<InvalidOperationException>(() => grandparent.SetParent(child));
    }

    [Fact]
    public void AddComponent_Then_GetComponent_Roundtrips()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("Enemy");

        var added = go.AddComponent<Health>();
        added.Value = 42;

        var fetched = go.GetComponent<Health>();

        Assert.NotNull(fetched);
        Assert.Same(added, fetched);
        Assert.Equal(42, fetched!.Value);
    }

    [Fact]
    public void GetComponent_Returns_Null_When_Absent()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("Empty");

        Assert.Null(go.GetComponent<Health>());
    }

    [Fact]
    public void Query_Finds_Only_GameObjects_Carrying_The_Component()
    {
        var world = new GameWorld();
        var withHealth = world.CreateGameObject("A");
        withHealth.AddComponent<Health>();
        var without = world.CreateGameObject("B");

        var matches = world.Query<Health>().ToList();

        Assert.Contains(withHealth, matches);
        Assert.DoesNotContain(without, matches);
    }

    [Fact]
    public void Query_Stops_Finding_A_GameObject_After_Its_Only_Component_Is_Removed()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("A");
        go.AddComponent<Health>();

        go.RemoveComponent<Health>();

        Assert.DoesNotContain(go, world.Query<Health>());
    }

    [Fact]
    public void Query_Still_Finds_A_GameObject_With_A_Duplicate_Component_After_One_Removal()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("A");
        go.AddComponent<Health>();
        go.AddComponent<Health>();

        go.RemoveComponent<Health>(); // removes one of the two

        Assert.Contains(go, world.Query<Health>());
    }

    [Fact]
    public void Destroy_Removes_The_GameObject_And_Its_Whole_Subtree()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        var child = world.CreateGameObject("Child");
        child.SetParent(parent);
        child.AddComponent<Health>();

        world.Destroy(parent);

        Assert.DoesNotContain(parent, world.Roots);
        Assert.DoesNotContain(child, world.Query<Health>());
    }

    [Fact]
    public void WorldMatrix_Composes_Local_Position_With_The_Parent_Chain()
    {
        var world = new GameWorld();
        var parent = world.CreateGameObject("Parent");
        parent.Transform.LocalPosition = new Vector3(1, 0, 0);
        var child = world.CreateGameObject("Child");
        child.Transform.LocalPosition = new Vector3(0, 1, 0);
        child.SetParent(parent);

        var worldPosition = child.WorldMatrix.Translation;

        Assert.Equal(new Vector3(1, 1, 0), worldPosition);
    }

    private sealed class Health : Component
    {
        public int Value;
    }
}
