using System.ComponentModel.DataAnnotations;

namespace UpKeep.Contracts.MaintenanceTasks;

public class UpdateMaintenanceTaskRequest
{
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateOnly? DueDate { get; set; }
}
