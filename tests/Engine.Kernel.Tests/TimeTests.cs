using Engine.Kernel.Diagnostics;

namespace Engine.Kernel.Tests;

public class TimeTests
{
    [Fact]
    public void Starts_At_Zero()
    {
        var time = new Time();

        Assert.Equal(0, time.DeltaTime);
        Assert.Equal(0, time.ElapsedTime);
        Assert.Equal(0, time.FrameCount);
    }

    [Fact]
    public void Tick_Sets_DeltaTime_And_Advances_ElapsedTime_And_FrameCount()
    {
        var time = new Time();

        time.Tick(0.5f);

        Assert.Equal(0.5f, time.DeltaTime);
        Assert.Equal(0.5, time.ElapsedTime, 3);
        Assert.Equal(1, time.FrameCount);
    }

    [Fact]
    public void ElapsedTime_And_FrameCount_Accumulate_Across_Ticks()
    {
        var time = new Time();

        time.Tick(0.5f);
        time.Tick(0.25f);

        Assert.Equal(0.25f, time.DeltaTime);
        Assert.Equal(0.75, time.ElapsedTime, 3);
        Assert.Equal(2, time.FrameCount);
    }

    [Fact]
    public void ConsumeFixedSteps_BelowOneStep_ReturnsZero()
    {
        var time = new Time();

        // FixedDeltaTime is 1/50 = 0.02s; a 60fps-ish frame is shorter.
        time.Tick(0.01f);

        Assert.Equal(0, time.ConsumeFixedSteps());
    }

    [Fact]
    public void ConsumeFixedSteps_ConsumesWholeStepsAndKeepsRemainder()
    {
        var time = new Time();

        time.Tick(0.05f); // 2.5 steps' worth
        Assert.Equal(2, time.ConsumeFixedSteps());

        time.Tick(0.03f); // remainder 0.01 + 0.03 = 0.04 -> 2 more steps
        Assert.Equal(2, time.ConsumeFixedSteps());
    }

    [Fact]
    public void ConsumeFixedSteps_CapsAtFiveEvenAfterAHugeStall()
    {
        var time = new Time();

        time.Tick(10f); // a debugger pause, not a real frame

        Assert.Equal(5, time.ConsumeFixedSteps());
    }
}
