using GamePlanner.API.Domain;

namespace GamePlanner.Tests;

public class PlaytimeCalculatorTests
{
    [Theory]
    [InlineData(42, 12, 42)]
    [InlineData(0, 12.5, 12.5)]
    [InlineData(0, 0, 0)]
    public void TotalHours_PrefersSteamWhenPositive(double steam, double logged, double expected) =>
        Assert.Equal(expected, PlaytimeCalculator.TotalHours(steam, logged));
}
