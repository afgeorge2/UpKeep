using Microsoft.EntityFrameworkCore;
using UpKeep.Data;
using UpKeep.Models;

namespace UpKeep.Security;

public sealed class WorkspaceAccess(UpKeepDbContext db, CurrentUser user)
{
    public async Task<string> PersonalWorkspace()
    {
        var key = user.Key ?? throw new InvalidOperationException("Sign-in is required.");
        if (await db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == key && m.UserKey == key)) return key;
        var workspace = new Workspace { Id = key, Members = [new WorkspaceMember { WorkspaceId = key, UserKey = key }] };
        db.Workspaces.Add(workspace);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // A simultaneous first request can have created this same personal workspace.
            db.ChangeTracker.Clear();
            if (!await db.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == key && m.UserKey == key)) throw;
        }
        return key;
    }
}
