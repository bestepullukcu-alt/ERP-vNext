namespace Diten.AuthService.Infrastructure.Settings;

public sealed class ServiceIdentityTokenIssuerOptions
{
    public const string SectionName = "ServiceIdentityTokenIssuer";
    public string Issuer { get; set; } = string.Empty;
    public string ActiveKeyId { get; set; } = string.Empty;
    public string ActivePrivateKeyPem { get; set; } = string.Empty;
    public int TokenLifetimeSeconds { get; set; } = 300;
}
