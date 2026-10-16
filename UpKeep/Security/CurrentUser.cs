using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace UpKeep.Security;

public sealed class CurrentUser(IHttpContextAccessor accessor)
{
    public string? Key =>
        accessor.HttpContext?.User.Identity?.IsAuthenticated == true
            ? accessor.HttpContext.User.FindFirstValue(
                ClaimTypes.NameIdentifier)
            : null;

    public static string AccountKey(string issuer, string subject)
    {
        var stableIdentity = JsonSerializer.Serialize(
            new[] { issuer, subject });
        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(stableIdentity)));
    }
}
