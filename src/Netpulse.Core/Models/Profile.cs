namespace Netpulse.Core.Models;

public sealed class Profile
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Home";
    public string Description { get; set; } = "";
    public int PingIntervalMs { get; set; } = 1000;
    public int TimeoutMs { get; set; } = 1000;
    public int PayloadBytes { get; set; } = 32;
    public int Ttl { get; set; } = 128;
    public PingProtocol Protocol { get; set; } = PingProtocol.TcpFallbackAuto;
    public int TcpPort { get; set; } = 443;
    public int ConsecutiveTimeoutsForOutage { get; set; } = 5;
    public int ConsecutiveSuccessesToRecover { get; set; } = 3;
    public int PingWarnMs { get; set; } = 80;
    public int PingBadMs { get; set; } = 150;
    public double LossWarnPercent { get; set; } = 2;
    public double LossBadPercent { get; set; } = 10;
    public bool IsActive { get; set; }
    public List<Target> Targets { get; set; } = [];

    public IEnumerable<Target> EnabledTargets => Targets.Where(t => t.Enabled && !string.IsNullOrWhiteSpace(t.Host));

    public override string ToString() => Name;
}

public sealed class ProfileFile
{
    public int SchemaVersion { get; set; } = 1;
    public List<Profile> Profiles { get; set; } = [];
}
