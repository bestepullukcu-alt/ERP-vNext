namespace Diten.Platform.API.Configuration;

/// <summary>
/// MOD-0040-FU02 (RCS-001) — the consumer credentials of <c>POST /api/internal/record-codes/allocate</c>, one binding per
/// consumer service. The binding → service-name map is CODE (<c>TrustedRecordCodeCredentialAuthenticator</c>); the
/// configuration carries only the secrets and the record types each consumer may ask for. No value lives in the repo.
/// </summary>
public sealed class TrustedRecordCodeCredentialOptions
{
    public const string SectionName = "TrustedRecordCodeCredential";

    public TrustedRecordCodeCredentialBinding FinanceTreasury { get; set; } = new();

    public TrustedRecordCodeCredentialBinding Mdm { get; set; } = new();
}

public sealed class TrustedRecordCodeCredentialBinding
{
    public string Identifier { get; set; } = string.Empty;
    public string ActiveSecret { get; set; } = string.Empty;
    public string? PreviousSecret { get; set; }
    public DateTimeOffset? PreviousValidUntilUtc { get; set; }
    public bool IsRevoked { get; set; }
    public string ConsumerService { get; set; } = string.Empty;
    public string AllowedAudience { get; set; } = string.Empty;
    public List<string> AllowedRecordTypes { get; set; } = [];
}
