using System.Text.Json;
using Netpulse.Core.Json;
using Netpulse.Core.Models;
using Netpulse.Core.Profiles;

namespace Netpulse.Tests;

public class ProfileJsonTests
{
    [Fact]
    public void RoundTrip()
    {
        var file = DefaultProfiles.Create();
        var json = JsonSerializer.Serialize(file, JsonDefaults.Options);
        var back = JsonSerializer.Deserialize<ProfileFile>(json, JsonDefaults.Options);
        Assert.NotNull(back);
        Assert.Equal(file.Profiles.Count, back!.Profiles.Count);
        Assert.Equal(file.Profiles[0].Name, back.Profiles[0].Name);
        Assert.Equal(file.Profiles[0].Targets.Count, back.Profiles[0].Targets.Count);
    }

    [Fact]
    public void ImportKeepsActiveProfile()
    {
        var home = DefaultProfiles.Make("Home", "", true);
        var work = DefaultProfiles.Make("Work", "", false);
        work.Targets.Clear();
        var file = new ProfileFile { Profiles = [work, home] };

        var got = ProfileMigration.Import(file);

        Assert.Equal(home.Id, got.Id);
        Assert.Equal(home.Targets.Count, got.Targets.Count);
    }

    [Fact]
    public void ImportSeedsWhenMissing()
    {
        var got = ProfileMigration.Import(null);
        Assert.Contains(got.Targets, t => t.Host == "1.1.1.1");
    }
}
