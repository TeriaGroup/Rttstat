using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;

namespace Netpulse.Core.Monitoring;

public readonly record struct TtlReply(bool Ok, bool TtlExpired, string Ip, double? RttMs, string Status);

public static class IcmpTtl
{
    public static IntPtr OpenHandle() => IcmpCreateFile();

    public static void CloseHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero && handle != new IntPtr(-1))
            IcmpCloseHandle(handle);
    }

    public static bool HandleOk(IntPtr handle) => handle != IntPtr.Zero && handle != new IntPtr(-1);

    public static TtlReply Ping(IPAddress dest, byte ttl, int timeoutMs, byte[] payload)
    {
        var handle = OpenHandle();
        if (!HandleOk(handle))
            return new TtlReply(false, false, "", null, "icmp-handle");
        try
        {
            return Ping(handle, dest, ttl, timeoutMs, payload);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static IntPtr OpenHandle6() => Icmp6CreateFile();

    public static TtlReply Ping6(IPAddress source, IPAddress dest, byte ttl, int timeoutMs, byte[] payload)
    {
        var handle = OpenHandle6();
        if (!HandleOk(handle))
            return new TtlReply(false, false, "", null, "no-ipv6");
        try
        {
            return Ping6(handle, source, dest, ttl, timeoutMs, payload);
        }
        finally
        {
            CloseHandle(handle);
        }
    }

    public static TtlReply Ping6(IntPtr handle, IPAddress source, IPAddress dest, byte ttl, int timeoutMs, byte[] payload)
    {
        if (!HandleOk(handle))
            return new TtlReply(false, false, "", null, "no-ipv6");
        if (dest.AddressFamily != AddressFamily.InterNetworkV6 || source.AddressFamily != AddressFamily.InterNetworkV6)
            return new TtlReply(false, false, "", null, "ipv6");

        var src = Sock(source);
        var dst = Sock(dest);
        var opts = new IpOptionInformation { Ttl = ttl };
        var reply = new byte[64 + payload.Length];
        var n = Icmp6SendEcho2(handle, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, ref src, ref dst, payload, (ushort)payload.Length, ref opts, reply, (uint)reply.Length, (uint)Math.Max(50, timeoutMs));
        if (n == 0)
            return new TtlReply(false, false, "", null, "timeout");

        var status = BitConverter.ToUInt32(reply, 28);
        var rtt = BitConverter.ToUInt32(reply, 32);
        var ip = FormatV6(reply, 8, BitConverter.ToUInt32(reply, 24));
        return status switch
        {
            0 => new TtlReply(true, false, ip, rtt, "ok"),
            11013 => new TtlReply(true, true, ip, rtt, "ttl"),
            11010 => new TtlReply(false, false, ip, null, "timeout"),
            11003 => new TtlReply(false, false, ip, rtt, "unreach"),
            _ => new TtlReply(false, false, ip, rtt, status.ToString())
        };
    }

    private static SockAddrIn6 Sock(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return new SockAddrIn6
        {
            Family = 23,
            A0 = bytes[0], A1 = bytes[1], A2 = bytes[2], A3 = bytes[3],
            A4 = bytes[4], A5 = bytes[5], A6 = bytes[6], A7 = bytes[7],
            A8 = bytes[8], A9 = bytes[9], A10 = bytes[10], A11 = bytes[11],
            A12 = bytes[12], A13 = bytes[13], A14 = bytes[14], A15 = bytes[15],
            ScopeId = (uint)address.ScopeId
        };
    }

    private static string FormatV6(byte[] reply, int offset, uint scope)
    {
        var raw = new byte[16];
        if (reply.Length < offset + 16) return "";
        Buffer.BlockCopy(reply, offset, raw, 0, 16);
        if (raw.All(b => b == 0)) return "";
        var ip = new IPAddress(raw, scope);
        return ip.ToString();
    }

    public static TtlReply Ping(IntPtr handle, IPAddress dest, byte ttl, int timeoutMs, byte[] payload)
    {
        if (!HandleOk(handle))
            return new TtlReply(false, false, "", null, "icmp-handle");
        if (dest.AddressFamily == AddressFamily.InterNetworkV6)
            return new TtlReply(false, false, "", null, "ipv6");
        if (dest.AddressFamily != AddressFamily.InterNetwork)
            return new TtlReply(false, false, "", null, "ipv6");

        var addr = BitConverter.ToUInt32(dest.GetAddressBytes(), 0);
        var opts = new IpOptionInformation { Ttl = ttl };
        var reply = new byte[Marshal.SizeOf<IcmpEchoReply>() + payload.Length + 32];
        var n = IcmpSendEcho(handle, addr, payload, (ushort)payload.Length, ref opts, reply, (uint)reply.Length, (uint)Math.Max(50, timeoutMs));
        if (n == 0)
            return new TtlReply(false, false, "", null, "timeout");

        var parsed = MemoryMarshal.Read<IcmpEchoReply>(reply);
        var ip = parsed.Address == 0 ? "" : new IPAddress(BitConverter.GetBytes(parsed.Address)).ToString();
        return parsed.Status switch
        {
            0 => new TtlReply(true, false, ip, parsed.RoundTripTime, "ok"),
            11013 => new TtlReply(true, true, ip, parsed.RoundTripTime, "ttl"),
            11010 => new TtlReply(false, false, ip, null, "timeout"),
            11003 => new TtlReply(false, false, ip, parsed.RoundTripTime, "unreach"),
            _ => new TtlReply(false, false, ip, parsed.RoundTripTime, parsed.Status.ToString())
        };
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern IntPtr Icmp6CreateFile();

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint Icmp6SendEcho2(
        IntPtr icmpHandle,
        IntPtr evt,
        IntPtr apcRoutine,
        IntPtr apcContext,
        ref SockAddrIn6 source,
        ref SockAddrIn6 destination,
        byte[] requestData,
        ushort requestSize,
        ref IpOptionInformation options,
        byte[] replyBuffer,
        uint replySize,
        uint timeout);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct SockAddrIn6
    {
        public ushort Family;
        public ushort Port;
        public uint FlowInfo;
        public byte A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10, A11, A12, A13, A14, A15;
        public uint ScopeId;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern IntPtr IcmpCreateFile();

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern bool IcmpCloseHandle(IntPtr handle);

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint IcmpSendEcho(
        IntPtr icmpHandle,
        uint destinationAddress,
        byte[] requestData,
        ushort requestSize,
        ref IpOptionInformation requestOptions,
        byte[] replyBuffer,
        uint replySize,
        uint timeout);

    [StructLayout(LayoutKind.Sequential)]
    private struct IpOptionInformation
    {
        public byte Ttl;
        public byte Tos;
        public byte Flags;
        public byte OptionsSize;
        public IntPtr OptionsData;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct IcmpEchoReply
    {
        public uint Address;
        public uint Status;
        public uint RoundTripTime;
        public ushort DataSize;
        public ushort Reserved;
        public IntPtr Data;
        public IpOptionInformation Options;
    }
}
