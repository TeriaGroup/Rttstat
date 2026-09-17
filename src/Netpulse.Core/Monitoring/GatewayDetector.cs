using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Netpulse.Core.Monitoring;

public sealed record AdapterInfo(
    string Id,
    string Name,
    string Description,
    bool Up,
    string Ipv4,
    string Gateway,
    bool IsLoopback,
    bool Virtual,
    NetworkInterfaceType Type);

public static class GatewayDetector
{
    private static readonly string[] VirtualMarks =
    [
        "radmin", "vpn", "tap", "tun", "wintun", "wireguard", "openvpn", "hamachi",
        "zerotier", "tailscale", "nordlynx", "anyconnect", "fortinet", "forticlient",
        "virtualbox", "vmware", "hyper-v", "hyperv", "vethernet", "teredo", "isatap",
        "6to4", "pseudo", "loopback", "npcap", "softether", "outline", "mullvad"
    ];

    public static IReadOnlyList<AdapterInfo> ListAdapters(bool includeDown)
    {
        var list = new List<AdapterInfo>();
        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
            if (nic.IsReceiveOnly) continue;
            var up = nic.OperationalStatus == OperationalStatus.Up;
            if (!up && !includeDown) continue;
            var props = nic.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";
            var gw = props.GatewayAddresses
                .FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString() ?? "";
            var virt = IsVirtual(nic.Name, nic.Description, nic.NetworkInterfaceType, ipv4, gw);
            list.Add(new AdapterInfo(nic.Id, nic.Name, nic.Description, up, ipv4, gw, false, virt, nic.NetworkInterfaceType));
        }
        return list;
    }

    public static AdapterInfo? PickDefault(string? preferredId, bool userChosen = false)
    {
        var all = ListAdapters(true);
        if (userChosen && !string.IsNullOrWhiteSpace(preferredId))
        {
            var match = all.FirstOrDefault(a => a.Id == preferredId);
            if (match is not null) return match;
        }

        return all
            .Where(a => a.Up)
            .OrderByDescending(Score)
            .FirstOrDefault()
            ?? all.FirstOrDefault();
    }

    public static int Score(AdapterInfo a)
    {
        var score = 0;
        if (a.Up) score += 10;
        if (!string.IsNullOrEmpty(a.Gateway)) score += 20;
        if (!a.Virtual) score += 50;
        if (a.Type is NetworkInterfaceType.Ethernet or NetworkInterfaceType.Wireless80211) score += 15;
        if (a.Type is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp) score -= 40;
        return score;
    }

    public static bool IsVirtual(string name, string description, NetworkInterfaceType type, string ipv4, string gateway)
    {
        if (type is NetworkInterfaceType.Tunnel or NetworkInterfaceType.Ppp or NetworkInterfaceType.Loopback)
            return true;
        var text = $"{name} {description}".ToLowerInvariant();
        if (VirtualMarks.Any(m => text.Contains(m, StringComparison.Ordinal)))
            return true;
        if (IsRadminRange(ipv4) || IsRadminRange(gateway))
            return true;
        return false;
    }

    public static bool IsMetered() => false;

    private static bool IsRadminRange(string ip)
    {
        if (!IPAddress.TryParse(ip, out var addr) || addr.AddressFamily != AddressFamily.InterNetwork)
            return false;
        var b = addr.GetAddressBytes();
        return b[0] == 26;
    }
}
