using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UpKeep.Data;
using UpKeep.Models;

namespace UpKeep.Tests;

public sealed class UpKeepApiFactory : WebApplicationFactory<Program>
{
    private const string TestAuthenticationScheme = "UpKeepTests";
    private const string TestUserHeader = "X-UpKeep-Test-User";
    private readonly string _databaseName = $"UpKeepTests-{Guid.NewGuid()}";

    public HttpClient CreateAuthenticatedClient(
        string userKey = "upkeep-test-user")
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
        client.DefaultRequestHeaders.Add(TestUserHeader, userKey);
        return client;
    }

    public HttpClient CreateCsrfClient(
        string userKey = "upkeep-test-user")
    {
        var client = CreateAuthenticatedClient(userKey);
        var session = client
            .GetFromJsonAsync<TestSession>("/api/auth/session")
            .GetAwaiter()
            .GetResult();

        if (string.IsNullOrWhiteSpace(session?.CsrfToken))
        {
            client.Dispose();
            throw new InvalidOperationException(
                "The test session did not return a CSRF token.");
        }

        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", session.CsrfToken);
        return client;
    }

    public async Task<int> SeedLegacyPropertyAsync(
        string name,
        string address)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<UpKeepDbContext>();
        var property = new Property
        {
            Name = name,
            Address = address,
            WorkspaceId = null
        };

        dbContext.Properties.Add(property);
        await dbContext.SaveChangesAsync();
        return property.Id;
    }

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

            services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthenticationScheme;
                    options.DefaultChallengeScheme = TestAuthenticationScheme;
                    options.DefaultForbidScheme = TestAuthenticationScheme;
                })
                .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                    TestAuthenticationScheme,
                    _ => { });
        });
    }

    private sealed class TestAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(
            options,
            logger,
            encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(TestUserHeader, out var userKey) ||
                string.IsNullOrWhiteSpace(userKey))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }

            Claim[] claims =
            [
                new(ClaimTypes.NameIdentifier, userKey.ToString()),
                new(ClaimTypes.Name, "UpKeep test user")
            ];
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }

    private sealed record TestSession(string? CsrfToken);
}
