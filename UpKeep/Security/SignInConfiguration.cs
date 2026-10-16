namespace UpKeep.Security;

public sealed class SignInConfiguration
{
    public string Authority { get; set; } = "";
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string PublicOrigin { get; set; } = "https://localhost:7153";
    public bool Configured => !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret);

    public void Validate(bool development)
    {
        if (!Uri.TryCreate(PublicOrigin, UriKind.Absolute, out var origin) || origin.Scheme != "https" ||
            origin.AbsolutePath != "/" || origin.Query.Length > 0 || origin.Fragment.Length > 0 || origin.UserInfo.Length > 0)
            throw new InvalidOperationException("Authentication:PublicOrigin must be an HTTPS origin without a path.");
        PublicOrigin = origin.GetLeftPart(UriPartial.Authority);
        if (Configured && (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority) || authority.Scheme != "https"))
            throw new InvalidOperationException("Authentication:Authority must use HTTPS.");
        if (!development && !Configured)
            throw new InvalidOperationException("Configure the OpenID Connect provider before running in Production.");
    }
}
