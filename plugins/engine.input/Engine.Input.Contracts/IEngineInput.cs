using Silk.NET.Input;

namespace Engine.Input.Contracts;

/// <summary>
/// Not IInput — same reasoning as IEngineWindow not being IWindow: keep a
/// clear line between our own vocabulary and the library's, even where a
/// collision isn't imminent yet. Reuses Silk.NET's own <c>Key</c> enum
/// rather than inventing one — it's just data, same as exposing
/// IEngineWindow.Native directly.
/// </summary>
public interface IEngineInput
{
    bool IsKeyDown(Key key);

    /// <summary>
    /// The raw Silk.NET input context, same escape hatch as
    /// IEngineWindow.Native — needed here specifically so engine.editor can
    /// construct Silk.NET.OpenGL.Extensions.ImGui's ImGuiController, which
    /// takes an IInputContext directly rather than anything of ours.
    /// </summary>
    IInputContext Native { get; }
}
