using Microsoft.EntityFrameworkCore;
using UpKeep.Data;
using UpKeep.Models;

namespace UpKeep.Security;

public sealed class WorkspaceAccess(
    UpKeepDbContext dbContext,
    CurrentUser user)
{
    public async Task<string> GetPersonalWorkspaceId()
    {
        var userKey = user.Key ??
            throw new InvalidOperationException("Sign-in is required.");

        var membershipExists = await dbContext.WorkspaceMembers
            .AnyAsync(member =>
                member.WorkspaceId == userKey &&
                member.UserKey == userKey);

        if (!membershipExists)
        {
            var workspace = new Workspace
            {
                Id = userKey,
                Members =
                [
                    new WorkspaceMember
                    {
                        WorkspaceId = userKey,
                        UserKey = userKey
                    }
                ]
            };

            dbContext.Workspaces.Add(workspace);
            await dbContext.SaveChangesAsync();
        }

        var legacyProperties = await dbContext.Properties
            .IgnoreQueryFilters()
            .Where(property => property.WorkspaceId == null)
            .ToListAsync();

        if (legacyProperties.Count > 0)
        {
            foreach (var property in legacyProperties)
            {
                property.WorkspaceId = userKey;
            }

            await dbContext.SaveChangesAsync();
        }

        return userKey;
    }
}
