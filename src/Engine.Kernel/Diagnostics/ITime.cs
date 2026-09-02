namespace Engine.Kernel.Diagnostics;

/// <summary>
/// Frame clock. See docs/kernel-contract.md §2.
///
/// No fixed-step accumulator yet — that was in the original kernel scope
/// but nothing consumes it: building it now, with no physics system to
/// test it against, would be exactly the kind of untested speculative
/// machinery this project has avoided everywhere else. It arrives with
/// M4, alongside the Stage.FixedUpdate it would drive — a "FixedUpdate"
/// stage without a real fixed-timestep accumulator behind it would be
/// actively misleading, not just incomplete.
/// </summary>
public interface ITime
{
    /// <summary>Seconds since the previous frame's Update stage.</summary>
    float DeltaTime { get; }

    /// <summary>Seconds since the engine started.</summary>
    double ElapsedTime { get; }

    int FrameCount { get; }
}
