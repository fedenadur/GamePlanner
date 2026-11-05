using GamePlanner.API.Domain;

namespace GamePlanner.Tests;

public class MvpConstantsTests
{
    [Fact]
    public void DefaultDevices_Steam_IsPcAndSteamDeck() =>
        Assert.Equal(["PC", "SteamDeck"], Stores.DefaultDevices(Stores.Steam));

    [Fact]
    public void DefaultDevices_Physical_IsEmpty() =>
        Assert.Empty(Stores.DefaultDevices(Stores.Physical));

    [Fact]
    public void DefaultDevices_NintendoEShop_IsSwitch() =>
        Assert.Equal(["Switch"], Stores.DefaultDevices(Stores.NintendoEShop));

    [Fact]
    public void IsSelectable_General_IsFalse() =>
        Assert.False(DeviceTypes.IsSelectable(DeviceTypes.General));

    [Fact]
    public void IsActive_OnlyPlayingAndReplaying()
    {
        Assert.True(JournalStatuses.IsActive(JournalStatuses.Playing));
        Assert.True(JournalStatuses.IsActive(JournalStatuses.Replaying));
        Assert.False(JournalStatuses.IsActive(JournalStatuses.Finished));
    }
}
