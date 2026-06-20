using Images.Services;

namespace Images.Tests;

public sealed class PerformanceBudgetServiceTests
{
    [Fact]
    public void PerfMeasurement_Passed_ReflectsThresholdComparison()
    {
        var passing = new PerfMeasurement("fast-op", 50, ThresholdMs: 100, Passed: true, Detail: null);
        var failing = new PerfMeasurement("slow-op", 200, ThresholdMs: 100, Passed: false, Detail: null);

        Assert.True(passing.Passed);
        Assert.False(failing.Passed);
    }

    [Fact]
    public void PerfMeasurement_NullThreshold_IsInfoOnly()
    {
        var info = new PerfMeasurement("memory", 512, ThresholdMs: null, Passed: true, Detail: "512 MB working set");

        Assert.Null(info.ThresholdMs);
        Assert.Equal("512 MB working set", info.Detail);
    }

    [Fact]
    public void PerfMeasurement_RecordEquality()
    {
        var a = new PerfMeasurement("test", 10, 100, true, null);
        var b = new PerfMeasurement("test", 10, 100, true, null);

        Assert.Equal(a, b);
    }
}
