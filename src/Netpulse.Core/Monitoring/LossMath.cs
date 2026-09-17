namespace Netpulse.Core.Monitoring;

public static class LossMath
{
    public static double Percent(int failures, int attempts)
    {
        if (attempts <= 0) return 0;
        return failures * 100.0 / attempts;
    }

    public static double CombineAvg(double oldAvg, int oldCount, double sample)
    {
        if (oldCount <= 0) return sample;
        return (oldAvg * oldCount + sample) / (oldCount + 1);
    }

    public static double Mbps(long bytes, double seconds)
    {
        if (seconds <= 0) return 0;
        return bytes * 8.0 / seconds / 1_000_000.0;
    }
}
