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
}
