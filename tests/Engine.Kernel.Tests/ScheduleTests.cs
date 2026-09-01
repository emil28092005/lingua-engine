using Engine.Kernel.Scheduling;
using Engine.Kernel.World;

namespace Engine.Kernel.Tests;

public class ScheduleTests
{
    // Component identity is all these tests need — no fields to assign.
    private sealed class Position : Component;

    private sealed class Velocity : Component;

    [Fact]
    public void RunStage_Only_Runs_Systems_Registered_For_That_Stage()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        var updateRan = false;
        var renderRan = false;

        schedule.Add(Stage.Update, (IWorld _) => updateRan = true);
        schedule.Add(Stage.Render, (IWorld _) => renderRan = true);

        schedule.RunStage(Stage.Update, world);

        Assert.True(updateRan);
        Assert.False(renderRan);
    }

    [Fact]
    public void RunStage_Runs_Every_System_Registered_For_The_Stage()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        var runCount = 0;

        schedule.Add(Stage.Update, (IWorld _) => runCount++);
        schedule.Add(Stage.Update, (IWorld _) => runCount++);

        schedule.RunStage(Stage.Update, world);

        Assert.Equal(2, runCount);
    }

    [Fact]
    public void A_System_Reading_An_Undeclared_Component_Throws()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        world.CreateGameObject("A").AddComponent<Position>();

        schedule.Add(Stage.Update, (IWorld w) => w.Query<Position>()); // no Reads<Position>()

        Assert.Throws<InvalidOperationException>(() => schedule.RunStage(Stage.Update, world));
    }

    [Fact]
    public void A_System_Reading_A_Declared_Component_Does_Not_Throw()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        world.CreateGameObject("A").AddComponent<Position>();

        schedule.Add(Stage.Update, (IWorld w) => w.Query<Position>()).Reads<Position>();

        var exception = Record.Exception(() => schedule.RunStage(Stage.Update, world));

        Assert.Null(exception);
    }

    [Fact]
    public void A_System_Adding_A_Component_Without_Declaring_Writes_Throws()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        var go = world.CreateGameObject("A");

        schedule.Add(Stage.Update, (IWorld _) => go.AddComponent<Position>()); // no Writes<Position>()

        Assert.Throws<InvalidOperationException>(() => schedule.RunStage(Stage.Update, world));
    }

    [Fact]
    public void A_System_Adding_A_Component_With_Declared_Writes_Does_Not_Throw()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        var go = world.CreateGameObject("A");

        schedule.Add(Stage.Update, (IWorld _) => go.AddComponent<Position>()).Writes<Position>();

        var exception = Record.Exception(() => schedule.RunStage(Stage.Update, world));

        Assert.Null(exception);
    }

    [Fact]
    public void Enforcement_Does_Not_Apply_Outside_A_Running_System()
    {
        var world = new GameWorld();
        var go = world.CreateGameObject("A");

        // No Schedule, no RunStage — direct kernel-side manipulation, same
        // as editor code or scene construction. Must not throw.
        var exception = Record.Exception(() => go.AddComponent<Position>());

        Assert.Null(exception);
    }

    [Fact]
    public void Two_Systems_With_Disjoint_Declared_Access_Both_Run()
    {
        var schedule = new Schedule();
        var world = new GameWorld();
        var ran = new List<string>();

        schedule.Add(Stage.Update, (IWorld _) => ran.Add("velocity")).Writes<Velocity>();
        schedule.Add(Stage.Update, (IWorld _) => ran.Add("position")).Writes<Position>();

        schedule.RunStage(Stage.Update, world);

        Assert.Equal(["velocity", "position"], ran);
    }
}
