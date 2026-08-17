namespace Diten.Platform.Application.Contracts.Audit;

public interface IAuditRetentionPolicyResolver
{
    Task<AuditRetentionResolution> ResolveAsync(AuditRetentionResolutionRequest request, CancellationToken ct = default);
}
