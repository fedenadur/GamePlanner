namespace GamePlanner.API.Domain;

public class WhatToPlayRequest
{
    public string? Device { get; set; }
    public string SessionTime { get; set; } = "Any";
    public string GameLength { get; set; } = "Any";
    public List<string> Genres { get; set; } = [];
    public string? Mood { get; set; }
    public string? Perspective { get; set; }
    public List<string> Modes { get; set; } = [];
    public string Strategy { get; set; } = "Default";
}

public class WhatToPlayReasonDto
{
    public string Kind { get; set; } = "";
    public string Text { get; set; } = "";
}

public class WhatToPlayResultDto
{
    public int GameId { get; set; }
    public string Name { get; set; } = "";
    public string? CoverImageUrl { get; set; }
    public string Store { get; set; } = "";
    public double? LengthHours { get; set; }
    public double TotalHours { get; set; }
    public int Score { get; set; }
    public string Match { get; set; } = "Low";
    public List<WhatToPlayReasonDto> Reasons { get; set; } = [];
}
