namespace Engine.Kernel.Scheduling;

/// <summary>
/// Fixed, kernel-defined, not plugin-extensible — see "Frame stages" in
/// docs/kernel-contract.md §8. <see cref="Present"/> is the second addition
/// to the original {Update, Render} set, added for M3's editor: engine.
/// render splits its old single Clear+Draw+SwapBuffers system into a
/// Render-stage draw and a Present-stage swap specifically so engine.
/// editor's ImGui overlay — which has to run after the scene is drawn but
/// before the buffers swap — has somewhere to register that isn't a race
/// against load order. <see cref="FixedUpdate"/> is the third, arriving
/// with M4 exactly as ITime's own doc comment said it would: engine.physics
/// steps Box3D here, at ITime.FixedDeltaTime's fixed rate, run zero-or-more
/// times per frame by the accumulator Engine.Host drives — see
/// Time.ConsumeFixedSteps. Runs before Update each frame it fires, so
/// gameplay code reading a Rigidbody's position in Update sees this frame's
/// physics result, not last frame's.
/// </summary>
public enum Stage
{
    FixedUpdate,
    Update,
    Render,
    Present,
}
