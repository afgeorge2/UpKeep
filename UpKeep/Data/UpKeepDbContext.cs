using Microsoft.EntityFrameworkCore;
using UpKeep.Models;
using UpKeep.Security;

namespace UpKeep.Data;

public class UpKeepDbContext : DbContext
{
    private readonly CurrentUser _user;

    public UpKeepDbContext(
        DbContextOptions<UpKeepDbContext> options,
        CurrentUser user)
        : base(options)
    {
        _user = user;
    }

    public DbSet<Property> Properties => Set<Property>();
    public DbSet<MaintenanceTask> MaintenanceTasks => Set<MaintenanceTask>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkspaceMember>()
            .HasKey(member => new
            {
                member.WorkspaceId,
                member.UserKey
            });
        modelBuilder.Entity<WorkspaceMember>()
            .HasOne(member => member.Workspace)
            .WithMany(workspace => workspace.Members)
            .HasForeignKey(member => member.WorkspaceId);

        modelBuilder.Entity<Property>()
            .HasOne(property => property.Workspace)
            .WithMany()
            .HasForeignKey(property => property.WorkspaceId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Property>().HasQueryFilter(property =>
            _user.Key != null &&
            property.WorkspaceId != null &&
            WorkspaceMembers.Any(member =>
                member.WorkspaceId == property.WorkspaceId &&
                member.UserKey == _user.Key));
        modelBuilder.Entity<MaintenanceTask>().HasQueryFilter(task =>
            _user.Key != null &&
            task.Property.WorkspaceId != null &&
            WorkspaceMembers.Any(member =>
                member.WorkspaceId == task.Property.WorkspaceId &&
                member.UserKey == _user.Key));
    }
}
