using Netpulse.Core.Models;

namespace Netpulse.Core.Profiles;

public static class ProfileMigration
{
    public static Profile Import(ProfileFile? file)
    {
        var active = file?.Profiles.FirstOrDefault(p => p.IsActive)
                     ?? file?.Profiles.FirstOrDefault();
        if (active is null)
            return Seed();

        active.IsActive = true;
        active.Targets ??= [];
        if (active.Targets.Count == 0)
        {
            var donor = file!.Profiles.FirstOrDefault(p => p.Targets is { Count: > 0 });
            if (donor is not null)
                active.Targets = donor.Targets;
        }
        return active;
    }

    public static Profile Seed()
    {
        var profile = DefaultProfiles.Make("", "", true);
        profile.Name = "";
        profile.Targets ??= [];
        return profile;
    }
}
