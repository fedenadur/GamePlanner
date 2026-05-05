namespace GamePlanner.API.Entities;

public class ImportJob
{
    public int      Id             { get; set; }
    public int      UserId         { get; set; }
    public string   SteamId        { get; set; } = string.Empty;
    public string   Status         { get; set; } = ImportJobStatus.Pending;
    public int      Progress       { get; set; }
    public int      TotalGames     { get; set; }
    public int      ProcessedGames { get; set; }
    public string?  ErrorMessage   { get; set; }
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt   { get; set; }
}

public static class ImportJobStatus
{
    public const string Pending   = "Pending";
    public const string Running   = "Running";
    public const string Completed = "Completed";
    public const string Failed    = "Failed";
}
