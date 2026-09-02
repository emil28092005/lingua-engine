using Engine.Audio.Contracts;
using Engine.Kernel.Plugins;
using Engine.Kernel.Scheduling;
using Engine.Kernel.World;

namespace Engine.Audio;

/// <summary>
/// M4's audio plugin: miniaudio, over the shim in native/audio-native/ —
/// see that file's own doc comment for why nothing but scalars and a UTF-8
/// path string cross the P/Invoke boundary. One system, on Stage.Update
/// (not FixedUpdate — nothing about audio is physics-timed): sync new/
/// changed/removed AudioSources every frame.
/// </summary>
public sealed class AudioPlugin : IPlugin
{
    private AudioWorld? _world;

    public void Configure(IPluginContext ctx)
    {
        _world = new AudioWorld(ctx.Log, useNullBackend: false);
        ctx.Services.Provide<IAudioService>(new AudioService(_world));

        ctx.Schedule.Add(Stage.Update, Sync).Reads<AudioSource>();

        ctx.Log.Info("audio engine ready (miniaudio)");
    }

    public void Shutdown(IPluginContext ctx)
    {
        ctx.Schedule.RemoveAllFrom("engine.audio");
        ctx.Services.Revoke<IAudioService>();
        _world?.Dispose();
        _world = null;
    }

    private void Sync(IWorld world) => _world!.Sync(world);
}
