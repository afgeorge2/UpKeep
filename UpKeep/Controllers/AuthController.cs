using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UpKeep.Security;

namespace UpKeep.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthController(SignInConfiguration configuration, CurrentUser user, IAntiforgery antiforgery) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("session")]
    public IActionResult Session()
    {
        var authenticated = user.Key is not null;
        return Ok(new {
            authenticated, configured = configuration.Configured,
            signInUrl = configuration.PublicOrigin + "/api/auth/login",
            name = authenticated ? User.Identity?.Name : null,
            accountKey = user.Key,
            csrfToken = authenticated ? antiforgery.GetAndStoreTokens(HttpContext).RequestToken : null
        });
    }

    [AllowAnonymous]
    [HttpGet("login")]
    public IActionResult Login()
    {
        if (!configuration.Configured) return StatusCode(503, new { message = "Sign-in is not configured yet." });
        if (!Request.IsHttps) return Redirect(configuration.PublicOrigin + "/api/auth/login");
        return Challenge(new AuthenticationProperties { RedirectUri = configuration.PublicOrigin + "/" },
            OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
