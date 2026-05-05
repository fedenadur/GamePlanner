namespace GamePlanner.API.Entities;

public class WorkerStatus
{
    public int       Id            { get; set; }
    public string    Name          { get; set; } = string.Empty;
    public string    Status        { get; set; } = WorkerStatusValue.Idle;
    public DateTime? LastRunAt     { get; set; }
    public DateTime? LastSuccessAt { get; set; }
    public string?   LastError     { get; set; }
}

public static class WorkerStatusValue
{
    public const string Idle      = "Idle";
    public const string Running   = "Running";
    public const string Completed = "Completed";
    public const string Failed    = "Failed";
}
