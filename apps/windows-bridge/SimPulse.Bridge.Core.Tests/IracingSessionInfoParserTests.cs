using SimPulse.Bridge.Core.Adapters.Iracing;
using SimPulse.Domain;

namespace SimPulse.Bridge.Core.Tests;

public sealed class IracingSessionInfoParserTests
{
    [Fact]
    public void Parses_track_car_and_session_type_from_yaml_subset()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-sample.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml);

        Assert.True(info.TrackDisplayName.TryGet(out string? track));
        Assert.Equal("Okayama International Raceway", track);
        Assert.True(info.TrackId.TryGet(out string? trackId));
        Assert.Equal("166", trackId);
        Assert.True(info.VehicleId.TryGet(out string? vehicleId));
        Assert.Equal("mazda mx-5 cup", vehicleId);
        Assert.True(info.VehicleDisplayName.TryGet(out string? vehicleName));
        Assert.Equal("Mazda MX-5 Cup", vehicleName);
        Assert.True(info.SessionType.TryGet(out string? sessionType));
        Assert.Equal("Practice", sessionType);
    }

    [Fact]
    public void Missing_fields_are_unavailable()
    {
        IracingSessionInfo info = IracingSessionInfoParser.Parse("WeekendInfo:\n  WeekendOptions:\n    NumStarters: 1\n");

        Assert.Equal(DataPresence.Unavailable, info.TrackDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, info.VehicleId.Presence);
        Assert.Equal(DataPresence.Unavailable, info.SessionType.Presence);
    }
}
