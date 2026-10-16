namespace UpKeep.Contracts.MaintenanceTasks;

public class MaintenanceTaskResponse
{
    public int RepeatEveryMonths { get; set; }
    public string? StatusMessage { get; set; }
    public int EstimatedMinutes { get; set; } = 30;
    public string Supplies { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string LastPurchase { get; set; } = string.Empty;
    public bool RepeatFromCompletion { get; set; }
    public bool IsSkipped { get; set; }
    public string? PreviousCompletionNotes { get; set; }
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public bool IsCompleted { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? CompletionNotes { get; set; }
    public int PropertyId { get; set; }
}
