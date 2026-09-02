using Engine.Kernel.World;

namespace Engine.Editor;

/// <summary>
/// Shared, per-session editor state — currently just which GameObject the
/// Hierarchy panel last clicked, so the Inspector panel (built against this
/// same instance) knows what to show. One instance per EditorPlugin, not
/// static: reloading engine.editor should start with nothing selected, not
/// hold a reference to a GameObject that may not even exist anymore. The
/// same staleness can happen without a reload too — see EditorPlugin.
/// DrawUi's IsPlaying-transition check, which re-resolves this after
/// ExitPlay's Restore replaces every GameObject with a fresh instance.
/// </summary>
internal sealed class EditorState
{
    public GameObject? Selected { get; set; }
}
