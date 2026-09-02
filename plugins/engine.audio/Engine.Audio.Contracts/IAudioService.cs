using Engine.Kernel.World;

namespace Engine.Audio.Contracts;

/// <summary>
/// Gameplay-facing trigger for a GameObject's AudioSource — "play the
/// bounce sound now," not a field on AudioSource itself, same split
/// IPhysicsService makes between a Rigidbody's persistent data and an
/// action performed on it (see that interface's own doc comment).
/// </summary>
public interface IAudioService
{
    void Play(GameObject go);

    void Stop(GameObject go);

    bool IsPlaying(GameObject go);
}
