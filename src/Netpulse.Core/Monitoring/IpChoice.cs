using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace Netpulse.Core.Monitoring;

public static class IpChoice
{
    public static string NormalizeHost(string host)
    {
        var s = host.Trim();
        if (s.Length >= 2 && s[0] == '[' && s[^1] == ']')
            s = s[1..^1];
        return s;
    }

    public static IPAddress Unwrap(IPAddress address)
        => address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;

    public static IPAddress? Prefer(IPAddress? literal, IEnumerable<IPAddress> resolved, bool preferIpv6 = false)
    {
        if (literal is not null) return literal;
        var list = resolved.ToList();
        var v4 = list.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
        var v6 = list.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetworkV6);
        if (preferIpv6) return v6 ?? v4;
        return v4 ?? v6;
    }

    public static IPAddress? LocalIpv6(IPAddress dest)
    {
        var locals = NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.OperationalStatus == OperationalStatus.Up)
            .SelectMany(n => n.GetIPProperties().UnicastAddresses)
            .Select(a => a.Address)
            .Where(a => a.AddressFamily == AddressFamily.InterNetworkV6 && !IPAddress.IsLoopback(a))
            .ToList();
        return PickLocal(dest, locals);
    }

    public static IPAddress? PickLocal(IPAddress dest, IReadOnlyList<IPAddress> locals)
    {
        if (dest.AddressFamily != AddressFamily.InterNetworkV6) return null;
        if (dest.IsIPv6LinkLocal)
        {
            return locals.FirstOrDefault(a => a.IsIPv6LinkLocal && a.ScopeId == dest.ScopeId)
                   ?? locals.FirstOrDefault(a => a.IsIPv6LinkLocal);
        }
        return locals.FirstOrDefault(IsGlobalUnicast)
               ?? locals.FirstOrDefault(a => !a.IsIPv6LinkLocal && !a.IsIPv6Multicast);
    }

    public static bool IsGlobalUnicast(IPAddress address)
    {
        if (address.AddressFamily != AddressFamily.InterNetworkV6) return false;
        var b = address.GetAddressBytes();
        return (b[0] & 0xE0) == 0x20;
    }
}
