using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Netpulse.Core.Abstractions;
using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public sealed class RouteEngine : BackgroundService
{
    private readonly MonitorHub _hub;
    private readonly IProfileProvider _profiles;
    private readonly ISettingsProvider _settings;
    private readonly ILogger<RouteEngine> _log;
    private readonly Dictionary<int, HopAccumulator> _acc = [];
    private List<string> _lastIps = [];
    private int _knownHops = 8;
    private Guid _targetId;
    private readonly byte[] _payload = new byte[32];

    public RouteEngine(MonitorHub hub, IProfileProvider profiles, ISettingsProvider settings, ILogger<RouteEngine> log)
    {
        _hub = hub;
        _profiles = profiles;
        _settings = settings;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!_hub.Paused)
                    await RoundAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Route round failed");
            }

            try { await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken); }
            catch (OperationCanceledException) { break; }
        }
    }

    private async Task RoundAsync(CancellationToken ct)
    {
        var profile = _profiles.Active;
        var wanted = _hub.TraceTarget;
        if (wanted is null) return;
        var target = profile.Targets.FirstOrDefault(t => t.Id == wanted && t.Enabled && !string.IsNullOrWhiteSpace(t.Host));
        if (target is null) return;
        if (target.Id != _targetId)
        {
            _targetId = target.Id;
            _acc.Clear();
            _lastIps = [];
            _knownHops = 8;
        }

        IPAddress? ip;
        try
        {
            var host = IpChoice.NormalizeHost(target.Host);
            if (IPAddress.TryParse(host, out var parsed))
                ip = IpChoice.Unwrap(parsed);
            else
            {
                var entry = await Dns.GetHostEntryAsync(host, ct);
                ip = IpChoice.Prefer(null, entry.AddressList, _settings.Current.PreferIpv6);
                if (ip is not null) ip = IpChoice.Unwrap(ip);
            }
        }
        catch
        {
            Publish(target, true, "dns");
            return;
        }

        if (ip is null)
        {
            Publish(target, false, "dns");
            return;
        }

        if (ip.AddressFamily == AddressFamily.InterNetworkV6 && IpChoice.LocalIpv6(ip) is null)
        {
            Publish(target, false, "no-ipv6");
            return;
        }

        var maxTtl = Math.Clamp(_settings.Current.MaxTtl, 5, 30);
        var timeout = Math.Clamp(profile.TimeoutMs, 100, 3000);
        var probeTo = Math.Min(_knownHops + 1, maxTtl);
        var (hops, reached) = await Task.Run(() => Probe(ip, probeTo, timeout, ct), ct);
        if (reached)
            _knownHops = hops.Length;
        else if (hops.Any(h => h.TtlExpired))
            _knownHops = Math.Max(_knownHops, Array.FindLastIndex(hops, h => h.TtlExpired) + 2);

        if (!reached && _knownHops < maxTtl)
            _knownHops = Math.Min(maxTtl, _knownHops + 1);

        var ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var states = new List<HopLiveState>();
        var ips = new List<string>();

        for (var i = 0; i < hops.Length; i++)
        {
            if (reached && i >= _knownHops) break;
            var h = hops[i];
            if (!_acc.TryGetValue(i + 1, out var acc))
            {
                acc = new HopAccumulator();
                _acc[i + 1] = acc;
            }
            var okHop = h.Ok || h.TtlExpired;
            acc.Add(okHop && h.Ip.Length > 0, h.RttMs);
            var st = acc.Stats();
            var ipStr = string.IsNullOrEmpty(h.Ip) ? "*" : h.Ip;
            ips.Add(ipStr);
            var hopState = new HopLiveState
            {
                Hop = i + 1,
                Ip = ipStr,
                LastOk = okHop,
                LastRttMs = h.RttMs,
                AvgRtt = st.avg,
                MinRtt = st.min,
                MaxRtt = st.max,
                Jitter = st.jitter,
                LossPercent = st.loss,
                Sent = st.sent,
                Recv = st.recv,
                Status = h.Status,
                Spark = st.spark
            };
            states.Add(hopState);
            _hub.EmitRoute(new RouteSample
            {
                Ts = ts,
                ProfileId = profile.Id,
                TargetId = target.Id,
                Hop = i + 1,
                Ip = string.IsNullOrEmpty(h.Ip) ? null : h.Ip,
                RttMs = h.RttMs,
                Ok = okHop && h.Ip.Length > 0,
                Status = h.Status
            });
        }

        var destLoss = states.Count == 0 ? 100 : states[^1].LossPercent;
        foreach (var s in states)
            s.IntermediateOnlyLoss = PathDiagnosis.IsIcmpLimited(destLoss, s.LossPercent, s.Hop == states.Count, reached);

        if (RoutePolicy.IsRouteChange(_lastIps, ips) && _lastIps.Count > 0)
            _hub.EmitEvent(EventLevel.Warn, EventCategory.Route, "Route changed");
        _lastIps = ips;

        if (_settings.Current.ReverseDns)
            _ = ResolveNamesAsync(states);

        _hub.PublishRoute(new RouteSnapshot
        {
            TargetId = target.Id,
            TargetHost = target.Host,
            Tracing = !reached,
            Banner = reached ? "" : "tracing",
            Hops = states
        });
    }

    private (TtlReply[] Hops, bool Reached) Probe(IPAddress ip, int probeTo, int timeout, CancellationToken ct)
    {
        var hops = new TtlReply[probeTo];
        var v6 = ip.AddressFamily == AddressFamily.InterNetworkV6;
        var source = v6 ? IpChoice.LocalIpv6(ip) : null;
        if (v6 && source is null)
            return ([new TtlReply(false, false, "", null, "no-ipv6")], false);
        var handle = v6 ? IcmpTtl.OpenHandle6() : IcmpTtl.OpenHandle();
        try
        {
            if (!IcmpTtl.HandleOk(handle))
                return ([new TtlReply(false, false, "", null, v6 ? "no-ipv6" : "icmp-handle")], false);

            for (var ttl = 1; ttl <= probeTo; ttl++)
            {
                ct.ThrowIfCancellationRequested();
                hops[ttl - 1] = v6
                    ? IcmpTtl.Ping6(handle, source!, ip, (byte)ttl, timeout, _payload)
                    : IcmpTtl.Ping(handle, ip, (byte)ttl, timeout, _payload);
                if (hops[ttl - 1] is { Ok: true, TtlExpired: false, Status: "ok" })
                    return (hops[..ttl], true);
            }
        }
        finally
        {
            IcmpTtl.CloseHandle(handle);
        }

        return (hops, false);
    }

    private static async Task ResolveNamesAsync(List<HopLiveState> hops)
    {
        foreach (var h in hops)
        {
            if (h.Ip is "*" or "") continue;
            try
            {
                var e = await Dns.GetHostEntryAsync(h.Ip);
                h.Name = e.HostName;
            }
            catch { /* ignore */ }
        }
    }

    private void Publish(Target target, bool tracing, string banner)
        => _hub.PublishRoute(new RouteSnapshot { TargetId = target.Id, TargetHost = target.Host, Tracing = tracing, Banner = banner });
}
