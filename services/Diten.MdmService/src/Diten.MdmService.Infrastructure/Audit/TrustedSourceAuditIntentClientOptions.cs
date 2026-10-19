namespace Diten.MdmService.Infrastructure.Audit;

public sealed class TrustedSourceAuditIntentClientOptions
{
    public const string SectionName = "TrustedSourceAuditIntentClient";
    public string PlatformBaseUrl { get; init; } = string.Empty;
}
