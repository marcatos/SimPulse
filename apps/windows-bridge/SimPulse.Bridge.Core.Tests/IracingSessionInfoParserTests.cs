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

    [Fact]
    public void Resolves_player_car_and_session_from_yaml_lists()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml, driverCarIdx: 3, sessionNum: 1);

        Assert.True(info.VehicleId.TryGet(out string? vehicleId));
        Assert.Equal("mazda mx-5 cup", vehicleId);
        Assert.True(info.VehicleDisplayName.TryGet(out string? vehicleName));
        Assert.Equal("Mazda MX-5 Cup", vehicleName);
        Assert.True(info.SessionType.TryGet(out string? sessionType));
        Assert.Equal("Race", sessionType);
    }

    [Fact]
    public void Default_parse_uses_first_driver_and_session()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml);

        Assert.True(info.VehicleId.TryGet(out string? vehicleId));
        Assert.Equal("othercar", vehicleId);
        Assert.True(info.VehicleDisplayName.TryGet(out string? vehicleName));
        Assert.Equal("Other Car", vehicleName);
        Assert.True(info.SessionType.TryGet(out string? sessionType));
        Assert.Equal("Practice", sessionType);
    }

    [Fact]
    public void Unmatched_lookup_args_are_unavailable()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml, driverCarIdx: 99, sessionNum: 99);

        Assert.Equal(DataPresence.Unavailable, info.VehicleId.Presence);
        Assert.Equal(DataPresence.Unavailable, info.VehicleDisplayName.Presence);
        Assert.Equal(DataPresence.Unavailable, info.SessionType.Presence);
    }

    [Fact]
    public void Matched_driver_keeps_first_session_when_session_num_unset()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml, driverCarIdx: 3);

        Assert.True(info.VehicleId.TryGet(out string? vehicleId));
        Assert.Equal("mazda mx-5 cup", vehicleId);
        Assert.True(info.VehicleDisplayName.TryGet(out string? vehicleName));
        Assert.Equal("Mazda MX-5 Cup", vehicleName);
        Assert.True(info.SessionType.TryGet(out string? sessionType));
        Assert.Equal("Practice", sessionType);
    }

    [Fact]
    public void Matched_session_keeps_first_driver_when_car_idx_unset()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml, sessionNum: 1);

        Assert.True(info.VehicleId.TryGet(out string? vehicleId));
        Assert.Equal("othercar", vehicleId);
        Assert.True(info.VehicleDisplayName.TryGet(out string? vehicleName));
        Assert.Equal("Other Car", vehicleName);
        Assert.True(info.SessionType.TryGet(out string? sessionType));
        Assert.Equal("Race", sessionType);
    }

    [Fact]
    public void Unmatched_driver_only_leaves_session_first_entry()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml, driverCarIdx: 99);

        Assert.Equal(DataPresence.Unavailable, info.VehicleId.Presence);
        Assert.Equal(DataPresence.Unavailable, info.VehicleDisplayName.Presence);
        Assert.True(info.SessionType.TryGet(out string? sessionType));
        Assert.Equal("Practice", sessionType);
    }

    [Fact]
    public void Unmatched_session_only_leaves_driver_first_entry()
    {
        string yaml = File.ReadAllText(FixturePathHelper.FixturePath("iracing", "session-info-two-drivers.yaml"));

        IracingSessionInfo info = IracingSessionInfoParser.Parse(yaml, sessionNum: 99);

        Assert.True(info.VehicleId.TryGet(out string? vehicleId));
        Assert.Equal("othercar", vehicleId);
        Assert.True(info.VehicleDisplayName.TryGet(out string? vehicleName));
        Assert.Equal("Other Car", vehicleName);
        Assert.Equal(DataPresence.Unavailable, info.SessionType.Presence);
    }
}
