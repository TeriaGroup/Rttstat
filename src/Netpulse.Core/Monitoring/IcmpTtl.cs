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

    public static TtlReply Ping(IntPtr handle, IPAddress dest, byte ttl, int timeoutMs, byte[] payload)
    {
        if (!HandleOk(handle))
            return new TtlReply(false, false, "", null, "icmp-handle");
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
