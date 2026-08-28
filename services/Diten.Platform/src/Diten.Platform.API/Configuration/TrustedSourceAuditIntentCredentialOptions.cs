namespace Diten.Platform.API.Configuration;

public sealed class TrustedSourceAuditIntentCredentialOptions
{
    public const string SectionName = "TrustedSourceAuditIntentCredential";

    public ServiceCredentialOptions Mdm { get; set; } = new();

    public sealed class ServiceCredentialOptions
    {
        public string Identifier { get; set; } = string.Empty;
        public string ActiveSecret { get; set; } = string.Empty;
        public string? PreviousSecret { get; set; }
        public DateTimeOffset? PreviousValidUntilUtc { get; set; }
        public bool IsRevoked { get; set; }
        public string ConsumerService { get; set; } = string.Empty;
        public string AllowedAudience { get; set; } = string.Empty;
        public List<string> AllowedTenantIds { get; set; } = [];
    }
}
