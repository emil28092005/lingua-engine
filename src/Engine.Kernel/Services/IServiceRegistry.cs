namespace Engine.Kernel.Services;

/// <summary>
/// The control-plane channel between plugins: commands and resources, not
/// per-entity data. See the "two channels" table in docs/kernel-contract.md
/// §2 — and the rule right below it about what never belongs here.
/// </summary>
public interface IServiceRegistry
{
    void Provide<T>(T instance) where T : class;
    T Require<T>() where T : class;
    void Revoke<T>() where T : class;
}
