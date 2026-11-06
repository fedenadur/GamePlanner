using GamePlanner.API.Domain;
using GamePlanner.API.Managers.WhatToPlay;

namespace GamePlanner.Tests;

public class WhatToPlayScorerTests
{
    private static readonly string[] None = [];

    private static WhatToPlayCandidate C(
        int id, string name = "Game", string status = JournalStatuses.Backlog,
        string[]? genres = null, string[]? devices = null, double total = 0,
        double? length = null, double? critic = null, bool fav = false, bool nowPlaying = false,
        string[]? modes = null) =>
        new(id, name, null, Stores.Steam, devices ?? None, genres ?? None, None, None,
            modes ?? None, None, status, total, length, critic, fav, nowPlaying);

    private static WhatToPlayProfile P(
        string[]? fav = null, string[]? avoid = null, int? max = null,
        Dictionary<string, int>? counts = null) =>
        new(new HashSet<string>(fav ?? None, StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(avoid ?? None, StringComparer.OrdinalIgnoreCase),
            max, "Neutral", "Neutral", "Neutral",
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            counts ?? new Dictionary<string, int>());

    private static List<WhatToPlayResultDto> Rank(
        IEnumerable<WhatToPlayCandidate> cs, WhatToPlayRequest? r = null, WhatToPlayProfile? p = null, int take = 5) =>
        WhatToPlayScorer.Rank(cs, r ?? new WhatToPlayRequest(), p ?? P(), take);

    [Fact]
    public void Excludes_FinishedDroppedAndNowPlaying()
    {
        var res = Rank([
            C(1, status: JournalStatuses.Finished), C(2, status: JournalStatuses.Dropped),
            C(3, nowPlaying: true), C(4)]);
        Assert.Equal([4], res.Select(x => x.GameId));
    }

    [Fact]
    public void Excludes_NotPlayableOnDevice_ButEmptyListIsAny()
    {
        var res = Rank([
            C(1, devices: [DeviceTypes.Switch]), C(2, devices: [DeviceTypes.PC]), C(3)],
            new WhatToPlayRequest { Device = DeviceTypes.PC });
        Assert.Equal([2, 3], res.Select(x => x.GameId).OrderBy(x => x));
    }

    [Fact]
    public void Excludes_AvoidedGenre_UnlessRequested()
    {
        var cs = new[] { C(1, genres: ["Horror"]) };
        Assert.Empty(Rank(cs, p: P(avoid: ["Horror"])));
        Assert.Single(Rank(cs, new WhatToPlayRequest { Genres = ["horror"] }, P(avoid: ["Horror"])));
    }

    [Fact]
    public void Device_General_TreatsAllPlayable()
    {
        var res = Rank([C(1, devices: [DeviceTypes.Switch])], new WhatToPlayRequest { Device = DeviceTypes.General });
        Assert.Single(res);
    }

    [Fact]
    public void RequestedGenre_RanksFirst()
    {
        var res = Rank([C(1, "A", genres: ["Puzzle"]), C(2, "B", genres: ["Shooter"])],
            new WhatToPlayRequest { Genres = ["Shooter"] });
        Assert.Equal(2, res[0].GameId);
        Assert.Contains(res[0].Reasons, x => x.Kind == "Positive" && x.Text == "Shooter, as requested");
    }

    [Fact]
    public void LengthOutsideBucket_StillReturned_WithShorterThanRequestedReason()
    {
        var res = Rank([C(1, length: 50)], new WhatToPlayRequest { GameLength = "Short" });
        Assert.Single(res);
        Assert.Contains(res[0].Reasons, x => x.Kind == "Negative" && x.Text == "~50 h, longer than requested");
        var res2 = Rank([C(1, length: 5)], new WhatToPlayRequest { GameLength = "Long" });
        Assert.Contains(res2[0].Reasons, x => x.Kind == "Negative" && x.Text == "~5 h, shorter than requested");
    }

    [Fact]
    public void UnknownEverything_NoThrow_NeutralWithNoLengthDataReason()
    {
        var res = Rank([C(1)]);
        Assert.Single(res);
        Assert.Contains(res[0].Reasons, x => x.Kind == "Neutral" && x.Text == "No length data");
        Assert.Equal("Low", res[0].Match);
    }

    [Fact]
    public void Deterministic_SameInputSameOrder()
    {
        var cs = new[] { C(3, "beta"), C(1, "Alpha"), C(2, "alpha"), C(4, "Zed") };
        var a = Rank(cs).Select(x => x.GameId).ToList();
        var b = Rank(cs.Reverse()).Select(x => x.GameId).ToList();
        Assert.Equal(a, b);
        Assert.Equal([1, 2, 3, 4], a);
    }

    [Fact]
    public void Resume_PrefersStartedGames()
    {
        var res = Rank([C(1, "A"), C(2, "B", total: 4)], new WhatToPlayRequest { Strategy = "Resume" });
        Assert.Equal(2, res[0].GameId);
        Assert.Contains(res[0].Reasons, x => x.Text == "Already started (4 h)");
    }

    [Fact]
    public void Discover_PrefersRarelyPlayedGenre()
    {
        var counts = new Dictionary<string, int> { ["Shooter"] = 9, ["Puzzle"] = 0 };
        var res = Rank([C(1, "A", genres: ["Shooter"]), C(2, "B", genres: ["Puzzle"])],
            new WhatToPlayRequest { Strategy = "Discover" }, P(counts: counts));
        Assert.Equal(2, res[0].GameId);
        Assert.Contains(res[0].Reasons, x => x.Text == "A genre you rarely play");
    }

    [Fact]
    public void AboveMaxHours_Penalized()
    {
        var res = Rank([C(1, "A", length: 80), C(2, "B", length: 10)], p: P(max: 20));
        Assert.Equal(2, res[0].GameId);
        Assert.Contains(res[1].Reasons, x => x.Kind == "Negative" && x.Text == "~80 h, above your 20 h limit");
    }

    [Fact]
    public void EveryResult_HasAtLeastOneReason()
    {
        var res = Rank([C(1, length: 12, total: 3), C(2, genres: ["Puzzle"], length: 5), C(3, fav: true)]);
        Assert.All(res, x => Assert.NotEmpty(x.Reasons));
    }

    [Fact]
    public void Take_LimitsResults()
    {
        var cs = Enumerable.Range(1, 10).Select(i => C(i, $"G{i:00}")).ToList();
        Assert.Equal(5, Rank(cs).Count);
        Assert.Equal(3, Rank(cs, take: 3).Count);
    }

    [Fact]
    public void EmptyCandidates_ReturnsEmpty() => Assert.Empty(Rank([]));
}
