namespace Engine.Kernel.Scheduling;

/// <summary>
/// Open question (see the footer of docs/kernel-contract.md): whether the
/// set of stages is fixed or plugin-extensible. This is only enough to
/// make the §3 example compile — not a decision.
/// </summary>
public enum Stage
{
    Update,
    Render,
}
