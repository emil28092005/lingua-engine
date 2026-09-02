using Engine.Audio;
using Engine.Audio.Contracts;
using Engine.Kernel.Diagnostics;
using Engine.Kernel.World;

namespace Engine.Audio.Tests;

file sealed class RecordingLogger : ILogger
{
    public List<string> Warnings { get; } = [];
    public List<string> Errors { get; } = [];

    public void Info(string message) { }
    public void Warn(string message) => Warnings.Add(message);
    public void Error(string message) => Errors.Add(message);
}

public class AudioWorldTests
{
    private const string TestClip = "assets/test_tone.wav";

    // Always the null backend: real playback needs a real audio device,
    // which a test run shouldn't depend on or be heard making noise from.
    // See AudioWorld's own doc comment.
    private static AudioWorld NewWorld(ILogger log) => new(log, useNullBackend: true);

    [Fact]
    public void PlayOnAwake_StartsPlaybackAsSoonAsSourceIsSynced()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var audio = NewWorld(log);

        var go = world.CreateGameObject("Sfx");
        go.AddComponent<AudioSource>().ClipPath = TestClip;
        go.GetComponent<AudioSource>()!.PlayOnAwake = true;

        audio.Sync(world);

        Assert.True(audio.IsPlaying(go));
        Assert.Empty(log.Errors);
    }

    [Fact]
    public void WithoutPlayOnAwake_DoesNotAutoPlay_ButServicePlayStarts()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var audio = NewWorld(log);

        var go = world.CreateGameObject("Sfx");
        go.AddComponent<AudioSource>().ClipPath = TestClip;

        audio.Sync(world);
        Assert.False(audio.IsPlaying(go));

        audio.Play(go);
        Assert.True(audio.IsPlaying(go));
    }

    [Fact]
    public void Stop_StopsPlayback()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var audio = NewWorld(log);

        var go = world.CreateGameObject("Sfx");
        go.AddComponent<AudioSource>().ClipPath = TestClip;
        audio.Sync(world);

        audio.Play(go);
        Assert.True(audio.IsPlaying(go));

        audio.Stop(go);
        Assert.False(audio.IsPlaying(go));
    }

    [Fact]
    public void MissingClip_WarnsAndDoesNotThrow()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var audio = NewWorld(log);

        var go = world.CreateGameObject("Broken");
        go.AddComponent<AudioSource>().ClipPath = "assets/does_not_exist.wav";

        audio.Sync(world);
        audio.Sync(world); // a second sync shouldn't re-warn or throw either

        Assert.Single(log.Warnings);
        Assert.False(audio.IsPlaying(go));
    }

    [Fact]
    public void DestroyedGameObject_SoundCleanedUp_SubsequentSyncsStillWork()
    {
        var world = new GameWorld();
        var log = new RecordingLogger();
        using var audio = NewWorld(log);

        var go = world.CreateGameObject("Temp");
        go.AddComponent<AudioSource>().ClipPath = TestClip;
        audio.Sync(world);
        audio.Play(go);

        world.Destroy(go);
        audio.Sync(world); // should unload the now-orphaned sound, not throw

        // A second GameObject reusing the same clip proves the world is
        // still in a usable state after the cleanup above.
        var go2 = world.CreateGameObject("Temp2");
        go2.AddComponent<AudioSource>().ClipPath = TestClip;
        audio.Sync(world);
        audio.Play(go2);

        Assert.True(audio.IsPlaying(go2));
    }
}
