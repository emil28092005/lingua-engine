namespace Engine.Editor.Contracts;

/// <summary>
/// Play/Stop, backed by IWorld.Snapshot()/Restore() — see those doc
/// comments for why a scene-format snapshot, not a separate clone
/// mechanism, is what Play mode actually is. Engine.Host reads IsPlaying
/// each frame to decide whether to run Stage.Update at all: Edit mode
/// renders the scene but never ticks it, same as Unity's Scene view versus
/// Game view distinction. Exposed as a service (not something only
/// engine.editor's own UI calls) so a future non-UI driver — a test
/// harness, a CLI "play for N frames and dump" command — can drive Play
/// mode without depending on ImGui at all.
/// </summary>
public interface IPlayModeController
{
    bool IsPlaying { get; }

    void EnterPlay();

    void ExitPlay();
}
