using System.Net.NetworkInformation;
using Netpulse.Core.Monitoring;

namespace Netpulse.Tests;

public class AdapterVirtualTests
{
    [Theory]
    [InlineData("Radmin VPN", "Famatech Radmin VPN", "26.0.0.15", "26.0.0.1", true)]
    [InlineData("Ethernet", "Intel Adapter", "192.168.1.10", "192.168.1.1", false)]
    [InlineData("Wi-Fi", "Intel Wi-Fi", "10.0.0.5", "10.0.0.1", false)]
    [InlineData("WireGuard Tunnel", "WireGuard", "10.8.0.2", "10.8.0.1", true)]
    public void DetectsVirtual(string name, string desc, string ip, string gw, bool virt)
    {
        Assert.Equal(virt, GatewayDetector.IsVirtual(name, desc, NetworkInterfaceType.Ethernet, ip, gw));
    }

    [Fact]
    public void PhysicalScoresHigherThanRadmin()
    {
        var lan = new AdapterInfo("1", "Ethernet", "Intel", true, "192.168.1.10", "192.168.1.1", false, false, NetworkInterfaceType.Ethernet);
        var vpn = new AdapterInfo("2", "Radmin VPN", "Radmin", true, "26.0.0.5", "26.0.0.1", false, true, NetworkInterfaceType.Ethernet);
        Assert.True(GatewayDetector.Score(lan) > GatewayDetector.Score(vpn));
    }
}
