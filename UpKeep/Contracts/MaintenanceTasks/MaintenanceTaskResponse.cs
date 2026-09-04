namespace UpKeep.Contracts.MaintenanceTasks;

public class MaintenanceTaskResponse
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public int PropertyId { get; set; }
}
