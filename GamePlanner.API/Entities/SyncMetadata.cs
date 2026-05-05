namespace GamePlanner.API.Entities;

public class SyncMetadata
{
    public int Id { get; set; }
    public DateTime? LastSyncDate { get; set; }
    public bool InitialSyncCompleted { get; set; }
}
