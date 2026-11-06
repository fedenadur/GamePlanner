using GamePlanner.API.Domain;

namespace GamePlanner.Tests;

public class GameLengthTests
{
    [Theory]
    [InlineData(null, 600, 25.5, 25.5)]    // HLTB wins over the estimate
    [InlineData(123, 60, 12.0, 12.0)]      // HLTB wins over the Steam placeholder
    [InlineData(null, 600, null, 10.0)]    // estimate / 60
    [InlineData(null, 60, null, 1.0)]      // manual 1 h is a real estimate
    [InlineData(123, 600, null, 10.0)]     // Steam game with an edited estimate
    public void Hours_KnownLength(int? steamAppId, int estimatedMinutes, double? hltb, double expected) =>
        Assert.Equal(expected, GameLength.Hours(steamAppId, estimatedMinutes, hltb));

    [Theory]
    [InlineData(123, 60, null)]            // Steam placeholder without HLTB
    [InlineData(null, 0, null)]
    [InlineData(null, 0, 0.0)]             // HLTB zero is not a value
    public void Hours_Unknown(int? steamAppId, int estimatedMinutes, double? hltb) =>
        Assert.Null(GameLength.Hours(steamAppId, estimatedMinutes, hltb));
}
