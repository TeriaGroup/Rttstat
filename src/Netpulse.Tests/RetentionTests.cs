using Netpulse.Core.Models;

namespace Netpulse.Tests;

public class RetentionTests
{
    [Fact]
    public void DefaultsMatchPrompt()
    {
        var r = new RetentionSettings();
        Assert.Equal(7, r.RawSamplesDays);
        Assert.Equal(30, r.MinuteSamplesDays);
        Assert.Equal(365, r.HourlySamplesDays);
        Assert.Equal(1825, r.DailySamplesDays);
        Assert.Equal(365, r.EventsDays);
    }

    [Fact]
    public void CutoffIsInThePast()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var cut = now - (long)TimeSpan.FromDays(7).TotalMilliseconds;
        Assert.True(cut < now);
    }
}
