namespace UpKeep.Models
{
    public class MaintenanceTask
    {
    public int EstimatedMinutes { get; set; } = 30;
    public string Supplies { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
    public string LastPurchase { get; set; } = string.Empty;
    public bool RepeatFromCompletion { get; set; }
        public DateOnly? ScheduleAnchor { get; set; }
        public bool IsSkipped { get; set; }
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateOnly DueDate { get; set; }
        public int RepeatEveryMonths { get; set; }
        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public bool HasCreatedNextOccurrence { get; set; }
        public int? GeneratedFromTaskId { get; set; }
        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public bool HasBeenEdited { get; set; }
        [System.ComponentModel.DataAnnotations.ConcurrencyCheck]
        public bool IsCompleted { get; set; }
        public DateTimeOffset? CompletedAt { get; set; }
        public string? CompletionNotes { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
    }
}
