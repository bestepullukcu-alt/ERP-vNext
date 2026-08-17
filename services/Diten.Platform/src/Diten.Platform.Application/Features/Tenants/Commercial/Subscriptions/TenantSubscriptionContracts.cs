using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Tenants.Commercial.Subscriptions;

public sealed record TenantSubscriptionDto(
    Guid Id,
    Guid TenantId,
    Guid PlanId,
    string? PlanCode,
    string? PlanName,
    TenantSubscriptionStatus Status,
    DateTimeOffset? TrialStartDateUtc,
    DateTimeOffset? TrialEndDateUtc,
    DateTimeOffset? CurrentPeriodStartUtc,
    DateTimeOffset? CurrentPeriodEndUtc,
    DateTimeOffset? ActivatedAtUtc,
    DateTimeOffset? RenewedAtUtc,
    DateTimeOffset? CancelledAtUtc,
    DateTimeOffset? ExpiredAtUtc,
    DateTimeOffset? SuspendedAtUtc,
    bool CancelAtPeriodEnd,
    string? CancellationReason,
    string? Source,
    byte[] RowVersion);

public sealed record TenantSubscriptionHistoryDto(
    Guid SubscriptionId,
    TenantSubscriptionStatus Status,
    string? PlanCode,
    string? PlanName,
    DateTimeOffset? CurrentPeriodStartUtc,
    DateTimeOffset? CurrentPeriodEndUtc,
    DateTimeOffset ChangedAtUtc,
    string ChangedBy,
    string? Reason,
    string Action);

public sealed record TenantSubscriptionEntitlementSnapshotDto(
    Guid TenantId,
    bool IsActive,
    Guid? PlanId,
    string? PlanCode,
    string? PlanName,
    TenantSubscriptionStatus? Status,
    DateTimeOffset? CurrentPeriodEndUtc,
    IReadOnlyDictionary<string, decimal> Quotas,
    IReadOnlyList<string> IncludedFeatures,
    IReadOnlyList<string> IncludedModuleKeys);

public sealed record AssignPlanToTenantRequest(
    Guid PlanId,
    bool IsTrial,
    DateTimeOffset? TrialEndDateUtc,
    DateTimeOffset? CurrentPeriodStartUtc,
    DateTimeOffset? CurrentPeriodEndUtc,
    string? Source);

public sealed record CreateTenantSubscriptionRequest(
    Guid PlanId,
    bool IsTrial,
    DateTimeOffset? TrialEndDateUtc,
    DateTimeOffset? CurrentPeriodStartUtc,
    DateTimeOffset? CurrentPeriodEndUtc,
    string? Source);

public sealed record ActivateTenantSubscriptionRequest(
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    byte[]? RowVersion);

public sealed record RenewTenantSubscriptionRequest(
    DateTimeOffset NewPeriodEndUtc,
    byte[]? RowVersion);

public sealed record CancelTenantSubscriptionRequest(
    string CancellationReason,
    bool CancelAtPeriodEnd,
    byte[]? RowVersion);

public sealed record SuspendTenantSubscriptionRequest(
    string Reason,
    byte[]? RowVersion);

public sealed record ReactivateTenantSubscriptionRequest(
    string? Reason,
    byte[]? RowVersion);
