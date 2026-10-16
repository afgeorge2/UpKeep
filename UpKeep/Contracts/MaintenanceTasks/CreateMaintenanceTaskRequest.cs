using System.ComponentModel.DataAnnotations;

namespace UpKeep.Contracts.MaintenanceTasks;

public class CreateMaintenanceTaskRequest
{
    [Range(1, 1440)]
    public int EstimatedMinutes { get; set; } = 30;
    [StringLength(1000)]
    public string Supplies { get; set; } = string.Empty;
    [StringLength(3000)]
    public string Instructions { get; set; } = string.Empty;
    [StringLength(500)]
    public string LastPurchase { get; set; } = string.Empty;
    public bool RepeatFromCompletion { get; set; }
    [Range(0, 120)]
    public int RepeatEveryMonths { get; set; }
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public DateOnly? DueDate { get; set; }
}
