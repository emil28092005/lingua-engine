using Engine.Kernel.World;

namespace Engine.Editor;

/// <summary>
/// Shared, per-session editor state — currently just which GameObject the
/// Hierarchy panel last clicked, so the Inspector panel (built against this
/// same instance) knows what to show. One instance per EditorPlugin, not
/// static: reloading engine.editor should start with nothing selected, not
/// hold a reference to a GameObject that may not even exist anymore.
/// </summary>
internal sealed class EditorState
{
    public GameObject? Selected { get; set; }
}
