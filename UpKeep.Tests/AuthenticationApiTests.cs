using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Xunit;

namespace UpKeep.Tests;

public sealed class AuthenticationApiTests : IClassFixture<UpKeepApiFactory>
{
    private readonly UpKeepApiFactory _factory;

    public AuthenticationApiTests(UpKeepApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AnonymousUser_CannotAccessProperties()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/properties");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnonymousUser_CanReadUnconfiguredSessionStatus()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();

        Assert.False(session!.Authenticated);
        Assert.False(session.Configured);
        Assert.Null(session.AccountKey);
    }

    [Fact]
    public async Task AuthenticatedSession_ReturnsIdentityAndCsrfToken()
    {
        const string accountKey = "account-a";
        using var client = _factory.CreateAuthenticatedClient(accountKey);

        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content.ReadFromJsonAsync<SessionResponse>();
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : [];

        Assert.True(session!.Authenticated);
        Assert.Equal(accountKey, session.AccountKey);
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
        Assert.Contains(cookies, cookie =>
            cookie.StartsWith("__Host-UpKeep.Csrf=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AuthenticatedMutation_WithoutCsrfToken_ReturnsBadRequest()
    {
        using var client = _factory.CreateAuthenticatedClient();

        var response = await client.PostAsJsonAsync(
            "/api/properties",
            new
            {
                name = "Lake house",
                address = "123 Main Street"
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Users_CannotAccessAnotherUsersPropertyOrTasks()
    {
        using var owner = _factory.CreateCsrfClient("account-a");
        using var otherUser = _factory.CreateCsrfClient("account-b");

        var propertyResponse = await owner.PostAsJsonAsync(
            "/api/properties",
            new
            {
                name = "Owner home",
                address = "123 Private Street"
            });
        Assert.Equal(HttpStatusCode.Created, propertyResponse.StatusCode);
        var property = await propertyResponse.Content
            .ReadFromJsonAsync<PropertyResponse>();

        var taskResponse = await owner.PostAsJsonAsync(
            $"/api/properties/{property!.Id}/maintenance-tasks",
            new
            {
                title = "Private task",
                description = "Only the owner can see this",
                dueDate = new DateOnly(2027, 1, 15)
            });
        Assert.Equal(HttpStatusCode.Created, taskResponse.StatusCode);
        var task = await taskResponse.Content
            .ReadFromJsonAsync<TaskResponse>();

        var propertyAsOtherUser = await otherUser.GetAsync(
            $"/api/properties/{property.Id}");
        var taskAsOtherUser = await otherUser.GetAsync(
            $"/api/properties/{property.Id}/maintenance-tasks/{task!.Id}");

        Assert.Equal(HttpStatusCode.NotFound, propertyAsOtherUser.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, taskAsOtherUser.StatusCode);
    }

    [Fact]
    public async Task FirstAuthenticatedUser_AdoptsLegacyProperties()
    {
        var legacyPropertyId = await _factory.SeedLegacyPropertyAsync(
            "Existing home",
            "456 Legacy Lane");
        using var firstUser = _factory.CreateCsrfClient("legacy-owner");
        using var otherUser = _factory.CreateCsrfClient("other-account");

        var ownerResponse = await firstUser.GetAsync(
            $"/api/properties/{legacyPropertyId}");
        var otherResponse = await otherUser.GetAsync(
            $"/api/properties/{legacyPropertyId}");

        Assert.Equal(HttpStatusCode.OK, ownerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, otherResponse.StatusCode);
    }

    [Fact]
    public async Task Login_WhenProviderIsNotConfigured_ReturnsServiceUnavailable()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/login");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            response.StatusCode);
    }

    [Fact]
    public async Task DevelopmentWithoutProvider_UsesStableLocalIdentity()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureLogging(logging => logging.ClearProviders());
            });
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost")
            });

        var firstSession = await client.GetFromJsonAsync<SessionResponse>(
            "/api/auth/session");
        var secondSession = await client.GetFromJsonAsync<SessionResponse>(
            "/api/auth/session");

        Assert.True(firstSession!.Authenticated);
        Assert.False(firstSession.Configured);
        Assert.False(string.IsNullOrWhiteSpace(firstSession.AccountKey));
        Assert.Equal(firstSession.AccountKey, secondSession!.AccountKey);
    }

    [Fact]
    public async Task DevelopmentHttpSession_ReturnsCsrfToken()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureLogging(logging => logging.ClearProviders());
            });
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("http://localhost")
            });

        var response = await client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var session = await response.Content
            .ReadFromJsonAsync<SessionResponse>();
        Assert.True(session!.Authenticated);
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
    }

    [Fact]
    public async Task ProductionWithoutProvider_RefusesToStart()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Production");
                builder.ConfigureLogging(logging => logging.ClearProviders());
            });

        var exception = await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            using var client = factory.CreateClient();
            await client.GetAsync("/api/auth/session");
        });

        Assert.Contains(
            "Configure the OpenID Connect provider",
            exception.ToString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task ConfiguredLogin_ChallengesOpenIdConnectProvider()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.UseSetting(
                    "Authentication:Authority",
                    "https://identity.example");
                builder.UseSetting(
                    "Authentication:ClientId",
                    "upkeep-client");
                builder.UseSetting(
                    "Authentication:ClientSecret",
                    "test-secret");
                builder.UseSetting(
                    "Authentication:PublicOrigin",
                    "https://upkeep.example");
                builder.ConfigureTestServices(services =>
                    services.PostConfigure<OpenIdConnectOptions>(
                        OpenIdConnectDefaults.AuthenticationScheme,
                        options =>
                        {
                            var providerConfiguration =
                                new OpenIdConnectConfiguration
                            {
                                AuthorizationEndpoint =
                                    "https://identity.example/authorize",
                                TokenEndpoint =
                                    "https://identity.example/token",
                                Issuer = "https://identity.example"
                            };
                            options.Configuration = providerConfiguration;
                            options.ConfigurationManager =
                                new StaticConfigurationManager<
                                    OpenIdConnectConfiguration>(
                                    providerConfiguration);
                        }));
                builder.ConfigureLogging(logging => logging.ClearProviders());
            });
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                BaseAddress = new Uri("https://upkeep.example")
            });

        var response = await client.GetAsync("/api/auth/login");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "identity.example",
            response.Headers.Location?.Host);
    }

    [Fact]
    public async Task AuthenticatedLogout_ExpiresSessionCookie()
    {
        using var client = _factory.CreateCsrfClient();

        var response = await client.PostAsync(
            "/api/auth/logout",
            content: null);
        var cookies = response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values
            : [];

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Contains(cookies, cookie =>
            cookie.StartsWith(
                "__Host-UpKeep.Session=",
                StringComparison.Ordinal));
    }

    private sealed record SessionResponse(
        bool Authenticated,
        bool Configured,
        string? AccountKey,
        string? CsrfToken);

    private sealed record PropertyResponse(
        int Id,
        string Name,
        string Address);

    private sealed record TaskResponse(int Id);
}
