namespace Diten.Platform.API.Configuration;

public sealed class TrustedServiceTokenValidationOptions
{
    public const string SectionName = "TrustedServiceTokenValidation";

    public string Issuer { get; set; } = string.Empty;
    public string CurrentKeyId { get; set; } = string.Empty;
    public string CurrentPublicKeyPem { get; set; } = string.Empty;
    public string? PreviousKeyId { get; set; }
    public string? PreviousPublicKeyPem { get; set; }
    public DateTimeOffset? PreviousValidUntilUtc { get; set; }
}
