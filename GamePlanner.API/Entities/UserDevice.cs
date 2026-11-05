namespace GamePlanner.API.Entities;

public class UserDevice
{
    public int    UserId    { get; set; }
    public string Device    { get; set; } = string.Empty;
    public int    SortOrder { get; set; }
}
