using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Domain.Repositories;

public interface IQuotaUsageRepository
{
    Task<QuotaUsage> CreateAsync(QuotaUsage usage, CancellationToken ct = default);
    Task<QuotaUsage?> GetByTenantAndKeyAsync(Guid tenantId, string quotaKey, CancellationToken ct = default);
    Task<IReadOnlyList<QuotaUsage>> GetByTenantAsync(Guid tenantId, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid tenantId, string quotaKey, CancellationToken ct = default);
    Task<QuotaMutationResult> TryConsumeAtomicAsync(Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct = default);
    Task<QuotaMutationResult> TryReleaseAtomicAsync(Guid tenantId, string quotaKey, decimal amount, DateTimeOffset now, CancellationToken ct = default);
    Task<QuotaUsage?> ResetPeriodAsync(Guid tenantId, string quotaKey, DateTimeOffset periodStart, DateTimeOffset periodEnd, DateTimeOffset now, CancellationToken ct = default);
    Task<QuotaUsage?> UpdateLimitAsync(Guid tenantId, string quotaKey, decimal limitValue, Guid subscriptionId, Guid planId, string source, string? overrideSource, DateTimeOffset now, CancellationToken ct = default);
    Task<QuotaUsage?> SetCurrentValueAsync(Guid tenantId, string quotaKey, decimal currentValue, DateTimeOffset now, CancellationToken ct = default);
    Task<QuotaUsage?> MarkNotificationStateAsync(Guid tenantId, string quotaKey, bool warningSent, bool breachSent, DateTimeOffset now, CancellationToken ct = default);
}

public sealed record QuotaMutationResult(bool Applied, QuotaUsage? Usage);
