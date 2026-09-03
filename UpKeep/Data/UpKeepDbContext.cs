using Microsoft.EntityFrameworkCore;
using UpKeep.Models;

namespace UpKeep.Data;

public class UpKeepDbContext : DbContext
{
    public UpKeepDbContext(DbContextOptions<UpKeepDbContext> options)
        : base(options)
    {
    }

    public DbSet<Property> Properties => Set<Property>();
}
