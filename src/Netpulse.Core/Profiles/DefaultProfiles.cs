using Netpulse.Core.Models;

namespace Netpulse.Core.Profiles;

public static class DefaultProfiles
{
    public static ProfileFile Create()
    {
        var home = Make("Home", "Default home profile", true);
        var office = Make("Office", "Office / work profile", false);
        return new ProfileFile { SchemaVersion = 1, Profiles = [home, office] };
    }

    public static Profile Make(string name, string description, bool active)
    {
        return new Profile
        {
            Name = name,
            Description = description,
            IsActive = active,
            Targets =
            [
                new Target { DisplayName = "Gateway", Host = "192.168.1.1", Role = TargetRole.Gateway, Enabled = false },
                new Target { DisplayName = "Cloudflare DNS", Host = "1.1.1.1", Role = TargetRole.Dns },
                new Target { DisplayName = "Google DNS", Host = "8.8.8.8", Role = TargetRole.External }
            ]
        };
    }
}
