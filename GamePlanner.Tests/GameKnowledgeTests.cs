using GamePlanner.API.Managers.WhatToPlay;

namespace GamePlanner.Tests;

public class GameKnowledgeTests
{
    [Theory]
    [InlineData("Short", new[] { "Puzzle" }, SessionFit.Good)]
    [InlineData("short", new[] { "puzzle" }, SessionFit.Good)]
    [InlineData("Short", new[] { "Role-playing (RPG)" }, SessionFit.Poor)]
    [InlineData("Short", new[] { "Strategy" }, SessionFit.Poor)]
    [InlineData("Short", new[] { "Adventure" }, SessionFit.Neutral)]
    [InlineData("Short", new string[0], SessionFit.Neutral)]
    [InlineData("Long", new[] { "Strategy" }, SessionFit.Good)]
    [InlineData("Long", new[] { "Arcade" }, SessionFit.Neutral)]
    [InlineData("Long", new[] { "Adventure" }, SessionFit.Neutral)]
    [InlineData("Medium", new[] { "Puzzle" }, SessionFit.Neutral)]
    [InlineData("Medium", new[] { "RPG" }, SessionFit.Neutral)]
    [InlineData("Any", new[] { "Puzzle" }, SessionFit.Neutral)]
    public void Fit_matches_table(string sessionTime, string[] genres, SessionFit expected) =>
        Assert.Equal(expected, GameKnowledge.Fit(genres, sessionTime));

    [Theory]
    [InlineData(new[] { "Adventure" }, new string[0], true)]
    [InlineData(new[] { "role-playing (rpg)" }, new string[0], true)]
    [InlineData(new[] { "RPG" }, new string[0], true)]
    [InlineData(new[] { "Visual Novel" }, new string[0], true)]
    [InlineData(new[] { "Point-and-click" }, new string[0], true)]
    [InlineData(new[] { "Puzzle" }, new[] { "drama" }, true)]
    [InlineData(new[] { "Puzzle" }, new[] { "Horror" }, false)]
    [InlineData(new string[0], new string[0], false)]
    public void IsNarrative_matches_table(string[] genres, string[] themes, bool expected) =>
        Assert.Equal(expected, GameKnowledge.IsNarrative(genres, themes));

    [Theory]
    [InlineData(new[] { "Shooter" }, new string[0], true)]
    [InlineData(new[] { "fighting" }, new string[0], true)]
    [InlineData(new[] { "Hack and slash/Beat 'em up" }, new string[0], true)]
    [InlineData(new[] { "Puzzle" }, new[] { "Action" }, true)]
    [InlineData(new[] { "Puzzle" }, new[] { "Stealth" }, false)]
    [InlineData(new string[0], new string[0], false)]
    public void IsCombat_matches_table(string[] genres, string[] tags, bool expected) =>
        Assert.Equal(expected, GameKnowledge.IsCombat(genres, tags));

    [Theory]
    [InlineData(new[] { "Single player" }, true)]
    [InlineData(new[] { "single PLAYER", "Multiplayer" }, true)]
    [InlineData(new[] { "Multiplayer" }, false)]
    [InlineData(new string[0], false)]
    public void IsSinglePlayer_matches_table(string[] modes, bool expected) =>
        Assert.Equal(expected, GameKnowledge.IsSinglePlayer(modes));

    [Theory]
    [InlineData("chill", new[] { "Indie", "Simulation", "Puzzle", "Visual Novel" })]
    [InlineData("Focused", new[] { "Action", "Platform", "Fighting", "Shooter", "Roguelike" })]
    [InlineData("story", new[] { "RPG", "Adventure", "Role-playing (RPG)", "Point-and-click" })]
    [InlineData("coop", new[] { "Co-operative", "Multiplayer", "Party" })]
    [InlineData("unknown", new string[0])]
    public void MoodGenres_matches_map(string mood, string[] expected) =>
        Assert.Equal(expected, GameKnowledge.MoodGenres(mood));

    [Theory]
    [InlineData(null, null)]
    [InlineData(0.0, "Short")]
    [InlineData(9.99, "Short")]
    [InlineData(10.0, "Medium")]
    [InlineData(30.0, "Medium")]
    [InlineData(30.01, "Long")]
    public void LengthBucket_matches_boundaries(double? hours, string? expected) =>
        Assert.Equal(expected, GameKnowledge.LengthBucket(hours));
}
