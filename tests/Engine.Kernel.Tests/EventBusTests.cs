using Engine.Kernel.Events;

namespace Engine.Kernel.Tests;

public class EventBusTests
{
    private readonly record struct Ping(int Value);

    [Fact]
    public void Publish_Invokes_Every_Subscriber_For_That_Event_Type()
    {
        var bus = new EventBus();
        var received = new List<int>();

        bus.Subscribe<Ping>(p => received.Add(p.Value));
        bus.Subscribe<Ping>(p => received.Add(p.Value * 10));

        bus.Publish(new Ping(3));

        Assert.Equal([3, 30], received);
    }

    [Fact]
    public void Publish_With_No_Subscribers_Does_Not_Throw()
    {
        var bus = new EventBus();

        var exception = Record.Exception(() => bus.Publish(new Ping(1)));

        Assert.Null(exception);
    }

    [Fact]
    public void Publish_Does_Not_Invoke_Handlers_Subscribed_To_A_Different_Event_Type()
    {
        var bus = new EventBus();
        var otherReceived = false;

        bus.Subscribe<string>(_ => otherReceived = true);

        bus.Publish(new Ping(1));

        Assert.False(otherReceived);
    }

    [Fact]
    public void RemoveAllFrom_An_Id_With_No_Registered_Plugin_Is_A_No_Op()
    {
        var bus = new EventBus();

        var exception = Record.Exception(() => bus.RemoveAllFrom("nothing-registered"));

        Assert.Null(exception);
    }
}
