using Engine.Audio.Contracts;
using Engine.Kernel.World;

namespace Engine.Audio;

internal sealed class AudioService(AudioWorld world) : IAudioService
{
    public void Play(GameObject go) => world.Play(go);

    public void Stop(GameObject go) => world.Stop(go);

    public bool IsPlaying(GameObject go) => world.IsPlaying(go);
}
