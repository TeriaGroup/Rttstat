using System.Net;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class Ipv6ChoiceTests
{
    [Fact(Timeout = 3000)]
    public async Task LiteralV6_StaysV6_NamePrefersV4()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var v6 = IPAddress.Parse("2606:4700:4700::1111");
        var v4 = IPAddress.Parse("1.1.1.1");
        Assert.Equal(v6, IpChoice.Prefer(v6, [v4]));
        Assert.Equal(v4, IpChoice.Prefer(null, [v6, v4]));
        Assert.Equal(v6, IpChoice.Prefer(null, [v6, v4], preferIpv6: true));
        Assert.Equal(v6, IpChoice.Prefer(null, [v6]));
        Assert.True(IpChoice.IsGlobalUnicast(v6));
        Assert.False(IpChoice.IsGlobalUnicast(IPAddress.Parse("fe80::1")));
        await Task.CompletedTask;
    }

    [Fact(Timeout = 3000)]
    public async Task LocalPick_GlobalForGlobal_LinkForLink()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(2));
        var global = IPAddress.Parse("2001:db8::1");
        var link = IPAddress.Parse("fe80::1");
        var dest = IPAddress.Parse("2606:4700:4700::1111");
        Assert.Equal(global, IpChoice.PickLocal(dest, [link, global]));
        var linkDest = IPAddress.Parse("fe80::abcd");
        Assert.Equal(link, IpChoice.PickLocal(linkDest, [global, link]));
        await Task.CompletedTask;
    }

    [Fact(Timeout = 8000)]
    public async Task LiveIpv6Ttl_DoesNotChangeMachine()
    {
        using var timed = new TimedRestore(TimeSpan.FromSeconds(6));
        var dest = IPAddress.Parse("2606:4700:4700::1111");
        var src = IpChoice.LocalIpv6(dest);
        if (src is null) return;
        var reply = await Task.Run(() => IcmpTtl.Ping6(src, dest, 1, 1500, new byte[8]));
        Assert.True(reply.Status is "ttl" or "ok" or "timeout" or "unreach", $"ipv6 ttl probe status={reply.Status} ip={reply.Ip} src={src}");
        if (reply.Status is "ttl" or "ok")
            Assert.False(string.IsNullOrEmpty(reply.Ip));
    }
}
