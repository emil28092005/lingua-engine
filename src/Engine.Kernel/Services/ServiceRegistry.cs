namespace Engine.Kernel.Services;

public sealed class ServiceRegistry : IServiceRegistry
{
    private readonly Dictionary<Type, object> _services = [];

    public void Provide<T>(T instance) where T : class
    {
        if (!_services.TryAdd(typeof(T), instance))
            throw new InvalidOperationException(
                $"A service for '{typeof(T).FullName}' is already registered.");
    }

    public T Require<T>() where T : class
    {
        if (_services.TryGetValue(typeof(T), out var instance))
            return (T)instance;

        throw new InvalidOperationException(
            $"No service is registered for '{typeof(T).FullName}'.");
    }

    public bool TryRequire<T>(out T? instance) where T : class
    {
        if (_services.TryGetValue(typeof(T), out var found))
        {
            instance = (T)found;
            return true;
        }

        instance = null;
        return false;
    }

    // No-op if absent, deliberately: Shutdown() is expected to revoke
    // unconditionally, including services a partially-failed Configure()
    // never got around to providing.
    public void Revoke<T>() where T : class
        => _services.Remove(typeof(T));
}
