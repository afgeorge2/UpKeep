using Microsoft.EntityFrameworkCore;
using UpKeep.Models;
using UpKeep.Security;

namespace UpKeep.Data;

public class UpKeepDbContext : DbContext
{
    private readonly CurrentUser _user;
    public UpKeepDbContext(DbContextOptions<UpKeepDbContext> options, CurrentUser user)
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
        modelBuilder.Entity<WorkspaceMember>().HasKey(m => new { m.WorkspaceId, m.UserKey });
        modelBuilder.Entity<WorkspaceMember>().HasOne(m => m.Workspace).WithMany(w => w.Members)
            .HasForeignKey(m => m.WorkspaceId);
        modelBuilder.Entity<Property>().HasOne(p => p.Workspace).WithMany()
            .HasForeignKey(p => p.WorkspaceId).OnDelete(DeleteBehavior.Restrict);
        // Null workspace IDs are preserved legacy records, inaccessible until assigned by an administrator.
        modelBuilder.Entity<Property>().HasQueryFilter(p => _user.Key != null && p.WorkspaceId != null &&
            WorkspaceMembers.Any(m => m.WorkspaceId == p.WorkspaceId && m.UserKey == _user.Key));
        modelBuilder.Entity<MaintenanceTask>().HasQueryFilter(t => _user.Key != null && t.Property.WorkspaceId != null &&
            WorkspaceMembers.Any(m => m.WorkspaceId == t.Property.WorkspaceId && m.UserKey == _user.Key));
    }
}
