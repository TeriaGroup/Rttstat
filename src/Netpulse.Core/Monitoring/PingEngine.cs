using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Netpulse.Core.Models;

namespace Netpulse.Core.Monitoring;

public sealed class PingEngine : IDisposable
{
    private readonly Ping _ping = new();
    private readonly Dictionary<Guid, (IPAddress Ip, DateTimeOffset At)> _dns = [];

    public async Task<(bool Ok, double? Rtt, string Status, string Resolved, bool IcmpError)> ProbeAsync(
        Target target,
        Profile profile,
        bool forceTcp,
        CancellationToken ct)
    {
        var host = target.Host.Trim();
        IPAddress? ip = null;
        try
        {
            if (IPAddress.TryParse(host, out var parsed))
                ip = parsed;
            else
                ip = await ResolveAsync(target, ct);
        }
        catch (Exception ex)
        {
            return (false, null, "dns: " + ex.Message, "", false);
        }

        if (ip is null)
            return (false, null, "dns: unresolved", "", false);

        var resolved = ip.ToString();
        var useTcp = forceTcp || profile.Protocol == PingProtocol.TcpOnly;
        if (!useTcp)
        {
            try
            {
                var opts = new PingOptions(profile.Ttl, true);
                var payload = new byte[Math.Clamp(profile.PayloadBytes, 0, 1472)];
                var reply = await _ping.SendPingAsync(ip, TimeSpan.FromMilliseconds(profile.TimeoutMs), payload, opts, ct);
                if (reply.Status == IPStatus.Success)
                    return (true, reply.RoundtripTime, "ok", resolved, false);
                return (false, null, reply.Status.ToString(), resolved, false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                if (profile.Protocol == PingProtocol.Icmp)
                    return (false, null, "icmp-error", resolved, true);
                useTcp = true;
            }
        }

        return await TcpProbeAsync(ip, profile.TcpPort, profile.TimeoutMs, resolved, ct);
    }

    private static async Task<(bool Ok, double? Rtt, string Status, string Resolved, bool IcmpError)> TcpProbeAsync(
        IPAddress ip, int port, int timeoutMs, string resolved, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var tcp = new TcpClient();
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(timeoutMs);
            await tcp.ConnectAsync(ip, port, timeoutCts.Token);
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, "tcp", resolved, false);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return (false, null, "tcp-timeout", resolved, false);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionRefused)
        {
            sw.Stop();
            return (true, sw.Elapsed.TotalMilliseconds, "tcp-refused", resolved, false);
        }
        catch (Exception ex)
        {
            return (false, null, "tcp: " + ex.GetType().Name, resolved, false);
        }
    }

    private async Task<IPAddress?> ResolveAsync(Target target, CancellationToken ct)
    {
        var every = Math.Max(0, target.ResolveDnsEverySec);
        if (every > 0 && _dns.TryGetValue(target.Id, out var cached)
            && DateTimeOffset.UtcNow - cached.At < TimeSpan.FromSeconds(every))
            return cached.Ip;

        var entry = await Dns.GetHostEntryAsync(target.Host.Trim(), ct);
        var ip = entry.AddressList.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
                 ?? entry.AddressList.FirstOrDefault();
        if (ip is not null)
            _dns[target.Id] = (ip, DateTimeOffset.UtcNow);
        return ip;
    }

    public void Dispose() => _ping.Dispose();
}
