namespace Engine.Kernel.Scheduling;

/// <summary>
/// Fixed, kernel-defined, not plugin-extensible — see "Frame stages" in
/// docs/kernel-contract.md §8. <see cref="Present"/> is the second addition
/// to the original {Update, Render} set (after FixedUpdate was scoped for
/// M4), added for M3's editor: engine.render splits its old single
/// Clear+Draw+SwapBuffers system into a Render-stage draw and a
/// Present-stage swap specifically so engine.editor's ImGui overlay — which
/// has to run after the scene is drawn but before the buffers swap — has
/// somewhere to register that isn't a race against load order. Not a stage
/// added speculatively: SwapBuffers already needed to move somewhere real.
/// </summary>
public enum Stage
{
    Update,
    Render,
    Present,
}
