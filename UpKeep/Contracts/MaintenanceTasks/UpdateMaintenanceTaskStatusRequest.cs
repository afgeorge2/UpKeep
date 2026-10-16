namespace UpKeep.Contracts.MaintenanceTasks;

public class UpdateMaintenanceTaskStatusRequest
{
    public DateOnly? LocalDate { get; set; }
    public bool IsSkipped { get; set; }
    public bool IsCompleted { get; set; }
}
