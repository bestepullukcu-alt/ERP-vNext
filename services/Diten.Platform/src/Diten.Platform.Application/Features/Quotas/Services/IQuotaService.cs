using Diten.Platform.Application.Common;

namespace Diten.Platform.Application.Features.Quotas.Services;

public interface IQuotaService
{
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
