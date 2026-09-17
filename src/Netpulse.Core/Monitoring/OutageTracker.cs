using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public sealed class OutageTracker
{
    private int _failStreak;
    private int _okStreak;
    private DateTimeOffset? _nicDownSince;

    public Outage? Current { get; private set; }

    public Outage? OnTick(
        Profile profile,
        bool adapterUp,
        IReadOnlyList<TargetLiveState> targets,
        string adapterId)
    {
        var now = DateTimeOffset.UtcNow;
        if (!adapterUp)
        {
            _nicDownSince ??= now;
            if (now - _nicDownSince >= TimeSpan.FromSeconds(2) && Current is null)
            {
                Current = Open(profile, adapterId, OutageCause.NicDown, "Adapter down");
                return Current;
            }
        }
        else _nicDownSince = null;

        var internetTargets = targets.Where(t => t.Role is TargetRole.External or TargetRole.Dns).ToList();
        var allInternetFail = internetTargets.Count > 0 && internetTargets.All(t => !t.LastOk);
        var anyInternetOk = internetTargets.Any(t => t.LastOk);

        if (allInternetFail || !adapterUp)
        {
            _failStreak++;
            _okStreak = 0;
        }
        else if (anyInternetOk && adapterUp)
        {
            _okStreak++;
            _failStreak = 0;
        }

        if (Current is null && adapterUp && _failStreak >= profile.ConsecutiveTimeoutsForOutage && allInternetFail)
        {
            var gw = targets.FirstOrDefault(t => t.Role == TargetRole.Gateway);
            var cause = LinkQualityEvaluator.GuessCause(true, gw, targets);
            Current = Open(profile, adapterId, cause, "Consecutive failures");
            return Current;
        }

        if (Current is not null && adapterUp && _okStreak >= profile.ConsecutiveSuccessesToRecover)
        {
            Current.EndedAtUtc = now;
            var closed = Current;
            Current = null;
            _failStreak = 0;
            return closed;
        }

        return Current;
    }

    public Outage? CloseOnExit()
    {
        if (Current is null) return null;
        Current.EndedAtUtc = DateTimeOffset.UtcNow;
        Current.Cause = OutageCause.AppExit;
        var closed = Current;
        Current = null;
        return closed;
    }

    public void Reset()
    {
        _failStreak = 0;
        _okStreak = 0;
        _nicDownSince = null;
        Current = null;
    }

    private static Outage Open(Profile profile, string adapterId, OutageCause cause, string detail) => new()
    {
        StartedAtUtc = DateTimeOffset.UtcNow,
        Cause = cause,
        ProfileId = profile.Id,
        AdapterId = adapterId,
        Detail = detail
    };
}
