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

    /// <summary>For a caller — typically Engine.Host — that needs to know
    /// whether an optional service exists without treating its absence as
    /// an error. A plugin's own Configure()/Shutdown() should keep using
    /// Require(): a missing dependency there is a real configuration bug,
    /// not a legitimate maybe.</summary>
    bool TryRequire<T>(out T? instance) where T : class;

    void Revoke<T>() where T : class;
}
