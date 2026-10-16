namespace UpKeep.Security;

public sealed class SignInConfiguration
{
    public string Authority { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string PublicOrigin { get; set; } = "https://localhost:7153";

    public bool Configured =>
        !string.IsNullOrWhiteSpace(Authority) &&
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret);

    public void Validate(bool allowUnconfigured)
    {
        if (!Uri.TryCreate(
                PublicOrigin,
                UriKind.Absolute,
                out var publicOrigin) ||
            publicOrigin.Scheme != Uri.UriSchemeHttps ||
            publicOrigin.AbsolutePath != "/" ||
            publicOrigin.Query.Length > 0 ||
            publicOrigin.Fragment.Length > 0 ||
            publicOrigin.UserInfo.Length > 0)
        {
            throw new InvalidOperationException(
                "Authentication:PublicOrigin must be an HTTPS origin " +
                "without a path.");
        }

        PublicOrigin = publicOrigin.GetLeftPart(UriPartial.Authority);

        if (Configured &&
            (!Uri.TryCreate(Authority, UriKind.Absolute, out var authority) ||
             authority.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                "Authentication:Authority must use HTTPS.");
        }

        if (!allowUnconfigured && !Configured)
        {
            throw new InvalidOperationException(
                "Configure the OpenID Connect provider before " +
                "running in Production.");
        }
    }
}
