using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace UpKeep.Security;

public static class AuthenticationSetup
{
    public static void AddUpKeepSignIn(this WebApplicationBuilder builder)
    {
        var settings = builder.Configuration.GetSection("Authentication").Get<SignInConfiguration>() ?? new();
        settings.Validate(builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing"));
        builder.Services.AddSingleton(settings);
        builder.Services.AddHttpContextAccessor();
        builder.Services.AddScoped<CurrentUser>();
        builder.Services.AddScoped<WorkspaceAccess>();
        builder.Services.AddAntiforgery(options =>
        {
            options.HeaderName = "X-CSRF-TOKEN";
            options.Cookie.Name = "__Host-UpKeep.Csrf";
            options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
            options.Cookie.SameSite = SameSiteMode.Strict;
        });
        var authentication = builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "__Host-UpKeep.Session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = false;
                options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
                options.Events.OnRedirectToAccessDenied = context => { context.Response.StatusCode = 403; return Task.CompletedTask; };
            });
        if (settings.Configured)
        {
            authentication.AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
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
                    context.ProtocolMessage.RedirectUri = settings.PublicOrigin + "/api/auth/callback";
                    // A fresh provider authentication is required after signing out of UpKeep.
                    context.ProtocolMessage.Prompt = "login";
                    return Task.CompletedTask;
                };
                options.Events.OnTokenValidated = context =>
                {
                    var subject = context.Principal?.FindFirstValue("sub");
                    var issuer = context.SecurityToken.Issuer;
                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(issuer))
                    {
                        context.Fail("The provider did not supply a stable identity.");
                        return Task.CompletedTask;
                    }
                    // Store only our own minimal identity, never provider roles or tokens.
                    var name = context.Principal?.FindFirstValue("name") ?? "UpKeep member";
                    context.Principal = new ClaimsPrincipal(new ClaimsIdentity(new[] {
                        new Claim(ClaimTypes.NameIdentifier, CurrentUser.AccountKey(issuer, subject)),
                        new Claim(ClaimTypes.Name, name)
                    }, CookieAuthenticationDefaults.AuthenticationScheme));
                    return Task.CompletedTask;
                };
                options.Events.OnRemoteFailure = context =>
                {
                    context.HandleResponse();
                    context.Response.Redirect(settings.PublicOrigin + "/?signin=failed");
                    return Task.CompletedTask;
                };
            });
        }
        builder.Services.AddAuthorization(options => options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser().RequireClaim(ClaimTypes.NameIdentifier).Build());
    }
}
