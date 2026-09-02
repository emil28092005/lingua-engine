namespace Engine.Kernel.Diagnostics;

/// <summary>
/// Read-only to plugins (ITime); advanced by whoever owns the frame loop —
/// Engine.Host today, headless or windowed — via <see cref="Tick"/>, which
/// isn't on the interface. Same split as Schedule/ISchedule: the host gets
/// the extra, plugin-facing code doesn't.
/// </summary>
public sealed class Time : ITime
{
    public float DeltaTime { get; private set; }
    public double ElapsedTime { get; private set; }
    public int FrameCount { get; private set; }

    public void Tick(float deltaTime)
    {
        DeltaTime = deltaTime;
        ElapsedTime += deltaTime;
        FrameCount++;
    }
}
