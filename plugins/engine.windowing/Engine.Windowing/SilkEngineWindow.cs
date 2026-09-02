using Engine.Windowing.Contracts;
using Silk.NET.Windowing;

namespace Engine.Windowing;

internal sealed class SilkEngineWindow(IWindow native) : IEngineWindow
{
    public IWindow Native { get; } = native;

    public bool IsClosing => Native.IsClosing;
}
