namespace Netpulse.Core.Models;

public sealed class Target
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string DisplayName { get; set; } = "";
    public string Host { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public TargetRole Role { get; set; } = TargetRole.Custom;
    public int Weight { get; set; } = 1;
    public int ResolveDnsEverySec { get; set; } = 60;
}
