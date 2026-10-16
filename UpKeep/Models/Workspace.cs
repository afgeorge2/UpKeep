using System.ComponentModel.DataAnnotations;

namespace UpKeep.Models;

public sealed class Workspace
{
    [MaxLength(64)] public string Id { get; set; } = "";
    [MaxLength(100)] public string Name { get; set; } = "My household";
    public ICollection<WorkspaceMember> Members { get; set; } = [];
}

public sealed class WorkspaceMember
{
    [MaxLength(64)] public string WorkspaceId { get; set; } = "";
    [MaxLength(64)] public string UserKey { get; set; } = "";
    public Workspace Workspace { get; set; } = null!;
}
