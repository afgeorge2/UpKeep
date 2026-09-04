using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UpKeep.Data;

namespace UpKeep.Tests;

public sealed class UpKeepApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"UpKeepTests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureLogging(logging => logging.ClearProviders());
        builder.ConfigureServices(services =>
        {
            var databaseOptions = services.SingleOrDefault(service =>
                service.ServiceType ==
                typeof(DbContextOptions<UpKeepDbContext>));

            if (databaseOptions is not null)
            {
                services.Remove(databaseOptions);
            }

            services.AddDbContext<UpKeepDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));
        });
    }
}
