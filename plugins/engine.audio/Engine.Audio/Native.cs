using System.Runtime.InteropServices;

namespace Engine.Audio;

/// <summary>
/// Matches native/audio-native/lingua_audio.c's exports one-to-one. Same
/// choice as engine.physics' Native.cs: classic DllImport, not
/// LibraryImport, so no AllowUnsafeBlocks is needed anywhere in this
/// plugin — every parameter here is a plain scalar or a UTF-8 string,
/// nothing that needs unsafe marshalling code.
/// </summary>
internal static class Native
{
    private const string Lib = "lingua_audio";

    [DllImport(Lib)]
    public static extern int Lingua_Audio_Init(int useNullBackend);

    [DllImport(Lib)]
    public static extern void Lingua_Audio_Shutdown();

    [DllImport(Lib)]
    public static extern int Lingua_Audio_LoadSound([MarshalAs(UnmanagedType.LPUTF8Str)] string path);

    [DllImport(Lib)]
    public static extern void Lingua_Audio_UnloadSound(int handle);

    [DllImport(Lib)]
    public static extern void Lingua_Audio_Play(int handle);

    [DllImport(Lib)]
    public static extern void Lingua_Audio_Stop(int handle);

    [DllImport(Lib)]
    public static extern void Lingua_Audio_SetVolume(int handle, float volume);

    [DllImport(Lib)]
    public static extern void Lingua_Audio_SetLooping(int handle, [MarshalAs(UnmanagedType.U1)] bool loop);

    [DllImport(Lib)]
    [return: MarshalAs(UnmanagedType.U1)]
    public static extern bool Lingua_Audio_IsPlaying(int handle);
}
