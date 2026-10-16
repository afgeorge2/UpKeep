using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace UpKeep.Security;

public static class AuthenticationSetup
{
    private const string LocalDevelopmentScheme =
        "UpKeepLocalDevelopment";

    public static IServiceCollection AddUpKeepAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var settings = configuration
            .GetSection("Authentication")
            .Get<SignInConfiguration>() ?? new SignInConfiguration();
        settings.Validate(
            environment.IsDevelopment() ||
            environment.IsEnvironment("Testing"));

        services.AddSingleton(settings);
        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUser>();
        services.AddScoped<WorkspaceAccess>();
        services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = environment.IsDevelopment()
                ? "UpKeep.Csrf"
                : "__Host-UpKeep.Csrf";
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                ? CookieSecurePolicy.SameAsRequest
                : CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        var defaultScheme =
            !settings.Configured && environment.IsDevelopment()
                ? LocalDevelopmentScheme
                : CookieAuthenticationDefaults.AuthenticationScheme;

        var authentication = services
            .AddAuthentication(defaultScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-UpKeep.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        if (defaultScheme == LocalDevelopmentScheme)
        {
            authentication.AddScheme<
                AuthenticationSchemeOptions,
                LocalDevelopmentAuthenticationHandler>(
                LocalDevelopmentScheme,
                _ => { });
        }

        if (settings.Configured)
        {
            authentication.AddOpenIdConnect(
                OpenIdConnectDefaults.AuthenticationScheme,
                options =>
                {
                    options.Authority = settings.Authority;
                    options.ClientId = settings.ClientId;
                    options.ClientSecret = settings.ClientSecret;
                    options.CallbackPath = "/api/auth/callback";
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.RequireHttpsMetadata = true;
                    options.MapInboundClaims = false;
                    options.SaveTokens = false;
                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");
                    options.TokenValidationParameters.NameClaimType = "name";
                    options.Events.OnRedirectToIdentityProvider = context =>
                    {
                        context.ProtocolMessage.RedirectUri =
                            settings.PublicOrigin + "/api/auth/callback";
                        context.ProtocolMessage.Prompt = "login";
                        return Task.CompletedTask;
                    };
                    options.Events.OnTokenValidated = context =>
                    {
                        var subject = context.Principal?
                            .FindFirstValue("sub");
                        var issuer = context.SecurityToken.Issuer;

                        if (string.IsNullOrWhiteSpace(subject) ||
                            string.IsNullOrWhiteSpace(issuer))
                        {
                            context.Fail(
                                "The provider did not supply a stable identity.");
                            return Task.CompletedTask;
                        }

                        var name = context.Principal?
                            .FindFirstValue("name") ?? "UpKeep member";
                        Claim[] claims =
                        [
                            new(
                                ClaimTypes.NameIdentifier,
                                CurrentUser.AccountKey(issuer, subject)),
                            new(ClaimTypes.Name, name)
                        ];
                        context.Principal = new ClaimsPrincipal(
                            new ClaimsIdentity(
                                claims,
                                CookieAuthenticationDefaults
                                    .AuthenticationScheme));
                        return Task.CompletedTask;
                    };
                    options.Events.OnRemoteFailure = context =>
                    {
                        context.HandleResponse();
                        context.Response.Redirect(
                            settings.PublicOrigin + "/?signin=failed");
                        return Task.CompletedTask;
                    };
                });
        }

        services.AddAuthorization(options =>
        {
            options.FallbackPolicy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .RequireClaim(System.Security.Claims.ClaimTypes.NameIdentifier)
                .Build();
        });

        return services;
    }

    private sealed class LocalDevelopmentAuthenticationHandler(
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
            Claim[] claims =
            [
                new(
                    ClaimTypes.NameIdentifier,
                    CurrentUser.AccountKey(
                        "upkeep-local-development",
                        "local-user")),
                new(ClaimTypes.Name, "Local UpKeep user")
            ];
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return Task.FromResult(AuthenticateResult.Success(ticket));
        }
    }
}
