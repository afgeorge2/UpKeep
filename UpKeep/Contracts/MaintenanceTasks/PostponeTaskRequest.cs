using System.ComponentModel.DataAnnotations;
namespace UpKeep.Contracts.MaintenanceTasks;
public class PostponeTaskRequest
{
    [Required]
    public DateOnly? DueDate { get; set; }
}
