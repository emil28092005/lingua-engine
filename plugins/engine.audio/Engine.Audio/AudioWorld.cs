using Engine.Audio.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.World;

namespace Engine.Audio;

/// <summary>
/// Owns the one native miniaudio engine this plugin creates, and the
/// GameObject &lt;-&gt; loaded-sound handle mapping. <see cref="Sync"/> loads a
/// clip the first time it sees a GameObject's AudioSource, applies
/// Volume/Loop changes without re-touching the native side when nothing
/// changed, fires PlayOnAwake exactly once, and — same no-destruction-event
/// problem PhysicsWorld has — unloads sounds for GameObjects that no
/// longer have an AudioSource.
///
/// useNullBackend exists for exactly one reason: Engine.Audio.Tests uses
/// it. Real gameplay always gets a real device (see AudioPlugin) — nothing
/// about the null backend belongs in a shipped build's own choice.
/// </summary>
internal sealed class AudioWorld : IDisposable
{
    private readonly ILogger _log;
    private readonly Dictionary<GameObject, int> _handles = [];
    private readonly Dictionary<GameObject, (float Volume, bool Loop)> _lastSynced = [];
    private readonly HashSet<GameObject> _warnedFailedToLoad = [];

    public AudioWorld(ILogger log, bool useNullBackend)
    {
        _log = log;

        if (Native.Lingua_Audio_Init(useNullBackend ? 1 : 0) == 0)
            _log.Error("Failed to initialize the audio engine — no sounds will play.");
    }

    public void Sync(IWorld world)
    {
        var live = new HashSet<GameObject>();

        foreach (var go in world.Query<AudioSource>())
        {
            live.Add(go);
            var src = go.GetComponent<AudioSource>()!;

            if (_warnedFailedToLoad.Contains(go))
                continue;

            if (!_handles.TryGetValue(go, out var handle))
            {
                handle = Native.Lingua_Audio_LoadSound(src.ClipPath);
                if (handle < 0)
                {
                    _log.Warn($"'{go.Name}' AudioSource failed to load '{src.ClipPath}'.");
                    _warnedFailedToLoad.Add(go);
                    continue;
                }

                _handles[go] = handle;
                Native.Lingua_Audio_SetVolume(handle, src.Volume);
                Native.Lingua_Audio_SetLooping(handle, src.Loop);
                _lastSynced[go] = (src.Volume, src.Loop);

                if (src.PlayOnAwake)
                    Native.Lingua_Audio_Play(handle);

                continue;
            }

            var last = _lastSynced[go];
            if (last.Volume != src.Volume)
                Native.Lingua_Audio_SetVolume(handle, src.Volume);
            if (last.Loop != src.Loop)
                Native.Lingua_Audio_SetLooping(handle, src.Loop);

            _lastSynced[go] = (src.Volume, src.Loop);
        }

        foreach (var stale in _handles.Keys.Where(go => !live.Contains(go)).ToList())
        {
            Native.Lingua_Audio_UnloadSound(_handles[stale]);
            _handles.Remove(stale);
            _lastSynced.Remove(stale);
        }

        _warnedFailedToLoad.RemoveWhere(go => !live.Contains(go));
    }

    public void Play(GameObject go)
    {
        if (_handles.TryGetValue(go, out var handle))
            Native.Lingua_Audio_Play(handle);
    }

    public void Stop(GameObject go)
    {
        if (_handles.TryGetValue(go, out var handle))
            Native.Lingua_Audio_Stop(handle);
    }

    public bool IsPlaying(GameObject go) =>
        _handles.TryGetValue(go, out var handle) && Native.Lingua_Audio_IsPlaying(handle);

    public void Dispose()
    {
        foreach (var handle in _handles.Values)
            Native.Lingua_Audio_UnloadSound(handle);

        _handles.Clear();
        _lastSynced.Clear();
        Native.Lingua_Audio_Shutdown();
    }
}
