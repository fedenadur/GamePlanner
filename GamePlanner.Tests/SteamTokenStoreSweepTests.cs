using GamePlanner.API.Domain;
using GamePlanner.API.Services;

namespace GamePlanner.Tests;

/// <summary>
/// Proves that SteamTokenStore.RemoveExpired() shrinks the store while leaving live entries
/// redeemable, is idempotent, and is a no-op on an empty store. Pure in-memory -- no DI
/// container and no database involved.
/// </summary>
public class SteamTokenStoreSweepTests
{
    private static AuthResponse NewResponse() => new() { Token = Guid.NewGuid().ToString("N") };

    [Fact]
    public void RemoveExpired_ShrinksStore_ToOnlyLiveEntries()
    {
        var store = new SteamTokenStore();

        store.Store(NewResponse(), TimeSpan.FromMinutes(-1));
        store.Store(NewResponse(), TimeSpan.FromMinutes(-1));
        store.Store(NewResponse());
        store.Store(NewResponse());

        Assert.Equal(4, store.Count);

        var removed = store.RemoveExpired();

        Assert.Equal(2, removed);
        Assert.Equal(2, store.Count);
    }

    [Fact]
    public void RemoveExpired_NeverRemovesLiveEntries_TheyStillRedeem()
    {
        var store = new SteamTokenStore();

        store.Store(NewResponse(), TimeSpan.FromMinutes(-1));
        var liveCodeA = store.Store(NewResponse());
        store.Store(NewResponse(), TimeSpan.FromMinutes(-1));
        var liveCodeB = store.Store(NewResponse());

        store.RemoveExpired();

        Assert.NotNull(store.Redeem(liveCodeA));
        Assert.NotNull(store.Redeem(liveCodeB));
    }

    [Fact]
    public void RemoveExpired_IsIdempotent_SecondSweepRemovesNothing()
    {
        var store = new SteamTokenStore();

        store.Store(NewResponse(), TimeSpan.FromMinutes(-1));
        store.Store(NewResponse());

        store.RemoveExpired();
        var countAfterFirstSweep = store.Count;

        var removedOnSecondSweep = store.RemoveExpired();

        Assert.Equal(0, removedOnSecondSweep);
        Assert.Equal(countAfterFirstSweep, store.Count);
    }

    [Fact]
    public void RemoveExpired_OnEmptyStore_ReturnsZero()
    {
        var store = new SteamTokenStore();

        var removed = store.RemoveExpired();

        Assert.Equal(0, removed);
        Assert.Equal(0, store.Count);
    }
}
