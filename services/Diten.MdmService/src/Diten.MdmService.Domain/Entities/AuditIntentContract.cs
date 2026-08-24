namespace Diten.MdmService.Domain.Entities;

public static class AuditIntentContract
{
    public const string SourceService = "Diten.MDM";

    public static string BuildCentralIdempotencyKey(
        Guid tenantId,
        Guid intentId,
        string contractVersion)
    {
        if (tenantId == Guid.Empty || intentId == Guid.Empty || string.IsNullOrWhiteSpace(contractVersion))
        {
            throw new ArgumentException("Tenant, intent and contract version are required for central idempotency.");
        }

        return $"{SourceService}:{tenantId:N}:{intentId:N}:{contractVersion.Trim()}";
    }
}
