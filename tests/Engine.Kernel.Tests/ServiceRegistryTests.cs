using Engine.Kernel.Services;

namespace Engine.Kernel.Tests;

public class ServiceRegistryTests
{
    private interface IGreeter
    {
        string Greet();
    }

    private sealed class Greeter : IGreeter
    {
        public string Greet() => "hi";
    }

    [Fact]
    public void Provide_Then_Require_Returns_The_Same_Instance()
    {
        var registry = new ServiceRegistry();
        var greeter = new Greeter();

        registry.Provide<IGreeter>(greeter);

        Assert.Same(greeter, registry.Require<IGreeter>());
    }

    [Fact]
    public void Require_Throws_When_Nothing_Is_Registered()
    {
        var registry = new ServiceRegistry();

        Assert.Throws<InvalidOperationException>(registry.Require<IGreeter>);
    }

    [Fact]
    public void Provide_Throws_When_A_Service_For_The_Type_Already_Exists()
    {
        var registry = new ServiceRegistry();
        registry.Provide<IGreeter>(new Greeter());

        Assert.Throws<InvalidOperationException>(() => registry.Provide<IGreeter>(new Greeter()));
    }

    [Fact]
    public void TryRequire_Returns_False_And_Null_When_Nothing_Is_Registered()
    {
        var registry = new ServiceRegistry();

        var found = registry.TryRequire<IGreeter>(out var instance);

        Assert.False(found);
        Assert.Null(instance);
    }

    [Fact]
    public void TryRequire_Returns_True_And_The_Instance_When_Registered()
    {
        var registry = new ServiceRegistry();
        var greeter = new Greeter();
        registry.Provide<IGreeter>(greeter);

        var found = registry.TryRequire<IGreeter>(out var instance);

        Assert.True(found);
        Assert.Same(greeter, instance);
    }

    [Fact]
    public void Revoke_Is_A_No_Op_When_Nothing_Is_Registered()
    {
        var registry = new ServiceRegistry();

        var exception = Record.Exception(registry.Revoke<IGreeter>);

        Assert.Null(exception);
    }

    [Fact]
    public void Revoke_Removes_The_Service_So_Require_Throws_Afterward()
    {
        var registry = new ServiceRegistry();
        registry.Provide<IGreeter>(new Greeter());

        registry.Revoke<IGreeter>();

        Assert.Throws<InvalidOperationException>(registry.Require<IGreeter>);
    }
}
