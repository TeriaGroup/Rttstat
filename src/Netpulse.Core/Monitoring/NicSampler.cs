using System.Net.NetworkInformation;

namespace Netpulse.Core.Monitoring;

public sealed class NicSampler
{
    private string? _id;
    private long _prevRecv;
    private long _prevSent;
    private long _prevTs;
    private bool _primed;

    public (bool Up, double RecvBps, double SentBps, string Name, string Id) Sample(string? preferredId, bool userChosen = false)
    {
        var adapter = GatewayDetector.PickDefault(preferredId, userChosen);
        if (adapter is null)
            return (false, 0, 0, "", "");

        NetworkInterface? nic = null;
        foreach (var n in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (n.Id == adapter.Id) { nic = n; break; }
        }
        if (nic is null)
            return (false, 0, 0, adapter.Name, adapter.Id);

        var stats = nic.GetIPStatistics();
        var now = Environment.TickCount64;
        double recvBps = 0, sentBps = 0;
        if (_primed && _id == nic.Id && now > _prevTs)
        {
            var dt = (now - _prevTs) / 1000.0;
            recvBps = Math.Max(0, (stats.BytesReceived - _prevRecv) / dt);
            sentBps = Math.Max(0, (stats.BytesSent - _prevSent) / dt);
        }
        _id = nic.Id;
        _prevRecv = stats.BytesReceived;
        _prevSent = stats.BytesSent;
        _prevTs = now;
        _primed = true;
        return (nic.OperationalStatus == OperationalStatus.Up, recvBps, sentBps, nic.Name, nic.Id);
    }

    public void Reset()
    {
        _primed = false;
        _id = null;
    }
}
