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
}
