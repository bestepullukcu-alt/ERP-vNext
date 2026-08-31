using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Quotas.Services;

public interface IQuotaService
{
    Task<Response<IReadOnlyList<QuotaStatusDto>>> InitializeSubscriptionQuotasAsync(IPlatformTransactionSession session,
        Diten.Platform.Domain.Entities.TenantSubscription subscription,
        Diten.Platform.Domain.Entities.SubscriptionPlan plan, bool synchronizeExisting,
        string source, string reason, string actorId, string correlationId, CancellationToken ct) =>
        throw new PlatformTransactionUnavailableException("The quota service does not implement transaction-bound subscription quota synchronization.");
    Task<Response<QuotaMutationDto>> TryConsumeEntitlementAsync(IPlatformTransactionSession session, TryConsumeQuotaRequest request, CancellationToken ct) =>
        throw new PlatformTransactionUnavailableException(
            "The quota service does not implement transaction-bound physical-entitlement consume.");
    Task<Response<QuotaMutationDto>> ReleaseEntitlementAsync(IPlatformTransactionSession session, ReleaseQuotaRequest request, CancellationToken ct) =>
        throw new PlatformTransactionUnavailableException(
            "The quota service does not implement transaction-bound physical-entitlement release.");
    Task<Response<QuotaStatusDto>> RecalculateEntitlementAsync(IPlatformTransactionSession session, RecalculateQuotaUsageRequest request, CancellationToken ct) =>
        throw new PlatformTransactionUnavailableException(
            "The quota service does not implement transaction-bound physical-entitlement recalculation.");
    Task<bool> TryConsumeAsync(Guid tenantId, string quotaKey, decimal amount, CancellationToken ct);
    Task<QuotaStatusDto> GetStatusAsync(Guid tenantId, string quotaKey);
    Task ReleaseAsync(Guid tenantId, string quotaKey, decimal amount);
    Task<Response<IReadOnlyList<QuotaStatusDto>>> GetStatusesAsync(Guid tenantId, CancellationToken ct);
    Task<Response<QuotaStatusDto>> GetStatusResponseAsync(Guid tenantId, string quotaKey, CancellationToken ct);
    Task<Response<IReadOnlyList<QuotaStatusDto>>> InitializeTenantQuotasAsync(Guid tenantId, string source, string reason, string actorId, string correlationId, CancellationToken ct);
    Task<Response<IReadOnlyList<QuotaStatusDto>>> SyncTenantQuotaLimitsAsync(Guid tenantId, string source, string reason, string actorId, string correlationId, CancellationToken ct);
    Task<Response<QuotaMutationDto>> TryConsumeAsync(TryConsumeQuotaRequest request, CancellationToken ct);
    Task<Response<QuotaMutationDto>> ReleaseAsync(ReleaseQuotaRequest request, CancellationToken ct);
    Task<Response<QuotaStatusDto>> ResetPeriodAsync(ResetQuotaPeriodRequest request, CancellationToken ct);
    Task<Response<QuotaStatusDto>> RecalculateAsync(RecalculateQuotaUsageRequest request, CancellationToken ct);
}
