namespace Engine.Kernel.Diagnostics;

/// <summary>
/// Read-only to plugins (ITime); advanced by whoever owns the frame loop —
/// Engine.Host today, headless or windowed — via <see cref="Tick"/> and
/// <see cref="ConsumeFixedSteps"/>, neither on the interface. Same split as
/// Schedule/ISchedule: the host gets the extra, plugin-facing code doesn't.
/// </summary>
public sealed class Time : ITime
{
    // Capped so a real stall — a debugger pause, a slow disk load — can't
    // turn into a catch-up burst of hundreds of physics steps executing
    // back to back, each one still assuming a normal frame budget around
    // it. Five steps behind (0.1s at the default rate) already means
    // something else is wrong; better to visibly fall behind real time
    // than to freeze the process trying to simulate through it.
    private const int MaxStepsPerFrame = 5;

    private float _accumulator;

    public float DeltaTime { get; private set; }
    public double ElapsedTime { get; private set; }
    public int FrameCount { get; private set; }
    public float FixedDeltaTime { get; } = 1f / 50f;

    public void Tick(float deltaTime)
    {
        DeltaTime = deltaTime;
        ElapsedTime += deltaTime;
        FrameCount++;
    }

    /// <summary>
    /// Adds this frame's DeltaTime to the accumulator and returns how many
    /// FixedDeltaTime-sized steps it can pay for, having already deducted
    /// that much. Call once per frame, then run Stage.FixedUpdate that many
    /// times (zero is normal — most frames are shorter than FixedDeltaTime
    /// at 50Hz against a 60+fps display).
    /// </summary>
    public int ConsumeFixedSteps()
    {
        _accumulator += DeltaTime;

        var steps = 0;
        while (_accumulator >= FixedDeltaTime && steps < MaxStepsPerFrame)
        {
            _accumulator -= FixedDeltaTime;
            steps++;
        }

        return steps;
    }
}
