namespace UpKeep.Models
{
    public class MaintenanceTask
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateOnly DueDate { get; set; }
        public bool IsCompleted { get; set; }

        public int PropertyId { get; set; }
        public Property Property { get; set; } = null!;
    }
}
