using System.ComponentModel.DataAnnotations;

namespace UpKeep.Models;

public sealed class Workspace
{
    [MaxLength(64)]
    public string Id { get; set; } = string.Empty;

    public ICollection<WorkspaceMember> Members { get; set; } = [];
}

public sealed class WorkspaceMember
{
    [MaxLength(64)]
    public string WorkspaceId { get; set; } = string.Empty;

    [MaxLength(64)]
    public string UserKey { get; set; } = string.Empty;

    public Workspace Workspace { get; set; } = null!;
}
