namespace Engine.Kernel.Diagnostics;

/// <summary>
/// Frame clock. See docs/kernel-contract.md §2.
///
/// M4 adds the fixed-step accumulator this doc comment used to say was
/// missing: engine.physics needs to step Box3D at a constant rate
/// regardless of how the real frame rate wobbles, and a "FixedUpdate"
/// stage without a real accumulator behind it would be actively
/// misleading, not just incomplete. See Stage.FixedUpdate and
/// Time.ConsumeFixedSteps for the actual mechanism.
/// </summary>
public interface ITime
{
    /// <summary>Seconds since the previous frame's Update stage.</summary>
    float DeltaTime { get; }

    /// <summary>Seconds since the engine started.</summary>
    double ElapsedTime { get; }

    int FrameCount { get; }

    /// <summary>
    /// The constant step size Stage.FixedUpdate always runs at, regardless
    /// of the real frame rate — 1/50s. Not per-project configurable yet:
    /// nothing has needed a different rate, and a setting nobody's asked
    /// for is speculative the same way an unconsumed accumulator was.
    /// </summary>
    float FixedDeltaTime { get; }
}
