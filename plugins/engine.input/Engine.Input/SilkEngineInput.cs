using Engine.Input.Contracts;
using Silk.NET.Input;

namespace Engine.Input;

internal sealed class SilkEngineInput(IInputContext context) : IEngineInput
{
    public bool IsKeyDown(Key key) => context.Keyboards.Any(k => k.IsKeyPressed(key));
}
