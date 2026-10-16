namespace UpKeep.Models
{
    public class Property
    {
        public int Id { get; set; }
        [System.ComponentModel.DataAnnotations.MaxLength(64)]
        public string? WorkspaceId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Workspace? Workspace { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public ICollection<MaintenanceTask> MaintenanceTasks { get; set; } = [];
    }
}
