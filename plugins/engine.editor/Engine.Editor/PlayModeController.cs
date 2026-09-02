using System.Diagnostics;
using Engine.Editor.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.World;

namespace Engine.Editor;

/// <summary>
/// A snapshot taken on EnterPlay is the only state this needs — see
/// IWorld.Snapshot's doc comment for why that's Play mode's entire
/// mechanism, not a simplification of some richer one. Logs EnterPlay's
/// wall-clock cost: M3's "done when" criterion is entering Play in under
/// 100ms, already proven at the kernel level by WorldSnapshotTests'
/// 300-GameObject timing assertion — this is the same measurement taken
/// through the real editor path instead of a unit test, so a regression
/// specific to this plugin (not the kernel primitive) would show up here
/// even if the kernel test stays green.
/// </summary>
internal sealed class PlayModeController(IWorld world, ILogger log) : IPlayModeController
{
    private string? _snapshot;

    public bool IsPlaying => _snapshot is not null;

    public void EnterPlay()
    {
        if (IsPlaying)
            return;

        var stopwatch = Stopwatch.StartNew();
        _snapshot = world.Snapshot();
        stopwatch.Stop();
        log.Info($"Entered Play mode in {stopwatch.Elapsed.TotalMilliseconds:F1}ms");
    }

    public void ExitPlay()
    {
        if (!IsPlaying)
            return;

        world.Restore(_snapshot!);
        _snapshot = null;
        log.Info("Exited Play mode");
    }
}
