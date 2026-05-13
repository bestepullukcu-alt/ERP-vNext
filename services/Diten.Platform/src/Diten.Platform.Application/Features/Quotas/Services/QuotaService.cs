using Diten.Platform.Application.Common;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using Microsoft.Extensions.Logging;

namespace Diten.Platform.Application.Features.Quotas.Services;

public sealed class QuotaService : IQuotaService
{
    private readonly IQuotaUsageRepository _usageRepository;
    private readonly IQuotaEventRepository _eventRepository;
    private readonly ITenantSubscriptionRepository _subscriptionRepository;
    private readonly ISubscriptionPlanRepository _planRepository;
    private readonly ITenantRegistryRepository _tenantRepository;
    private readonly ITenantModuleEntitlementRepository _entitlementRepository;
    private readonly ILogger<QuotaService> _logger;

    public QuotaService(
        IQuotaUsageRepository usageRepository,
        IQuotaEventRepository eventRepository,
        ITenantSubscriptionRepository subscriptionRepository,
        ISubscriptionPlanRepository planRepository,
        ITenantRegistryRepository tenantRepository,
        ITenantModuleEntitlementRepository entitlementRepository,
        ILogger<QuotaService> logger)
    {
        _usageRepository = usageRepository;
        _eventRepository = eventRepository;
        _subscriptionRepository = subscriptionRepository;
        _planRepository = planRepository;
        _tenantRepository = tenantRepository;
        _entitlementRepository = entitlementRepository;
        _logger = logger;
    }

    public async Task<bool> TryConsumeAsync(Guid tenantId, string quotaKey, decimal amount, CancellationToken ct)
    {
        var response = await TryConsumeAsync(new TryConsumeQuotaRequest(
            tenantId,
            quotaKey,
            amount,
            "IQuotaService",
            null,
            null,
            "Runtime quota consume.",
            "System",
            Guid.NewGuid().ToString()), ct);

        return response.IsSuccessful && response.Data?.Applied == true;
    }

    public async Task<QuotaStatusDto> GetStatusAsync(Guid tenantId, string quotaKey)
    {
        var response = await GetStatusResponseAsync(tenantId, quotaKey, CancellationToken.None);
        if (!response.IsSuccessful || response.Data is null)
        {
            throw new InvalidOperationException(string.Join("; ", response.Errors));
        }

        return response.Data;
    }

    public async Task ReleaseAsync(Guid tenantId, string quotaKey, decimal amount)
    {
        var response = await ReleaseAsync(new ReleaseQuotaRequest(
            tenantId,
            quotaKey,
            amount,
            "IQuotaService",
            null,
            null,
            "Runtime quota release.",
            "System",
            Guid.NewGuid().ToString()), CancellationToken.None);

        if (!response.IsSuccessful)
        {
            throw new InvalidOperationException(string.Join("; ", response.Errors));
        }
    }

    public async Task<Response<IReadOnlyList<QuotaStatusDto>>> GetStatusesAsync(Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return Response<IReadOnlyList<QuotaStatusDto>>.Fail(QuotaErrorCodes.TenantRequired, 400);
        }

        var usages = await _usageRepository.GetByTenantAsync(tenantId, ct);
        return Response<IReadOnlyList<QuotaStatusDto>>.Success(usages.Select(ToStatus).ToList());
    }

    public async Task<Response<QuotaStatusDto>> GetStatusResponseAsync(Guid tenantId, string quotaKey, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.TenantRequired, 400);
        }

        if (!QuotaKeys.IsKnown(quotaKey))
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.KeyUnknown, 400);
        }

        var usage = await _usageRepository.GetByTenantAndKeyAsync(tenantId, quotaKey, ct);
        return usage is null
            ? Response<QuotaStatusDto>.Fail(QuotaErrorCodes.UsageNotFound, 404)
            : Response<QuotaStatusDto>.Success(ToStatus(usage));
    }

    public async Task<Response<IReadOnlyList<QuotaStatusDto>>> InitializeTenantQuotasAsync(Guid tenantId, string source, string reason, string actorId, string correlationId, CancellationToken ct)
    {
        var limits = await ResolveLimitsAsync(tenantId, false, ct);
        if (!limits.IsSuccessful || limits.Subscription is null || limits.Plan is null || limits.Quotas is null)
        {
            return Response<IReadOnlyList<QuotaStatusDto>>.Fail(limits.ErrorCode ?? QuotaErrorCodes.ConfigurationMissing, limits.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;
        var statuses = new List<QuotaStatusDto>();
        foreach (var item in limits.Quotas.Where(x => QuotaKeys.IsKnown(x.Key)))
        {
            var normalizedKey = NormalizeKey(item.Key);
            var usage = await _usageRepository.GetByTenantAndKeyAsync(tenantId, normalizedKey, ct);
            if (usage is null)
            {
                usage = new QuotaUsage
                {
                    TenantId = tenantId,
                    QuotaKey = normalizedKey,
                    CurrentValue = 0,
                    LimitValue = item.Value,
                    PeriodStart = limits.Subscription.CurrentPeriodStartUtc ?? now,
                    PeriodEnd = limits.Subscription.CurrentPeriodEndUtc ?? now.AddMonths(1),
                    LastUpdatedUtc = now,
                    Source = QuotaLimitSources.PlanDefault,
                    SubscriptionId = limits.Subscription.Id,
                    PlanId = limits.Plan.Id,
                    CreatedBy = actorId
                };
                await _usageRepository.CreateAsync(usage, ct);
                await WriteEventAsync(tenantId, normalizedKey, 0, source, reason, null, null, false, null, ct);
            }

            statuses.Add(ToStatus(usage));
        }

        return Response<IReadOnlyList<QuotaStatusDto>>.Success(statuses);
    }

    public async Task<Response<IReadOnlyList<QuotaStatusDto>>> SyncTenantQuotaLimitsAsync(Guid tenantId, string source, string reason, string actorId, string correlationId, CancellationToken ct)
    {
        var limits = await ResolveLimitsAsync(tenantId, false, ct);
        if (!limits.IsSuccessful || limits.Subscription is null || limits.Plan is null || limits.Quotas is null)
        {
            return Response<IReadOnlyList<QuotaStatusDto>>.Fail(limits.ErrorCode ?? QuotaErrorCodes.ConfigurationMissing, limits.StatusCode);
        }

        var now = DateTimeOffset.UtcNow;
        var statuses = new List<QuotaStatusDto>();
        foreach (var item in limits.Quotas.Where(x => QuotaKeys.IsKnown(x.Key)))
        {
            var normalizedKey = NormalizeKey(item.Key);
            var usage = await _usageRepository.UpdateLimitAsync(
                tenantId,
                normalizedKey,
                item.Value,
                limits.Subscription.Id,
                limits.Plan.Id,
                QuotaLimitSources.PlanDefault,
                null,
                now,
                ct);

            if (usage is null)
            {
                usage = new QuotaUsage
                {
                    TenantId = tenantId,
                    QuotaKey = normalizedKey,
                    CurrentValue = 0,
                    LimitValue = item.Value,
                    PeriodStart = limits.Subscription.CurrentPeriodStartUtc ?? now,
                    PeriodEnd = limits.Subscription.CurrentPeriodEndUtc ?? now.AddMonths(1),
                    LastUpdatedUtc = now,
                    Source = QuotaLimitSources.PlanDefault,
                    SubscriptionId = limits.Subscription.Id,
                    PlanId = limits.Plan.Id,
                    CreatedBy = actorId
                };
                await _usageRepository.CreateAsync(usage, ct);
            }

            await WriteEventAsync(tenantId, normalizedKey, 0, source, reason, null, null, false, null, ct);
            statuses.Add(ToStatus(usage));
        }

        return Response<IReadOnlyList<QuotaStatusDto>>.Success(statuses);
    }

    public async Task<Response<QuotaMutationDto>> TryConsumeAsync(TryConsumeQuotaRequest request, CancellationToken ct)
    {
        var validation = ValidateMutationRequest(request.TenantId, request.QuotaKey, request.Amount, true);
        if (validation is not null)
        {
            await WriteEventAsync(request.TenantId, request.QuotaKey, request.Amount, request.Source, ReasonOrDefault(request.Reason, "Rejected quota consume."), request.OperationId, request.SourceReference, true, validation, ct);
            return Response<QuotaMutationDto>.Fail(validation, validation == QuotaErrorCodes.LimitExceeded ? 409 : 400);
        }

        var limits = await ResolveLimitsAsync(request.TenantId, true, ct);
        if (!limits.IsSuccessful)
        {
            await WriteEventAsync(request.TenantId, request.QuotaKey, request.Amount, request.Source, ReasonOrDefault(request.Reason, "Rejected quota consume."), request.OperationId, request.SourceReference, true, limits.ErrorCode, ct);
            return Response<QuotaMutationDto>.Fail(limits.ErrorCode ?? QuotaErrorCodes.ConfigurationMissing, limits.StatusCode);
        }

        if (await IsDuplicateAsync(request.TenantId, request.QuotaKey, request.Source, request.OperationId, request.SourceReference, false, ct))
        {
            return Response<QuotaMutationDto>.Fail(QuotaErrorCodes.DuplicateOperation, 409);
        }

        var now = DateTimeOffset.UtcNow;
        var result = await _usageRepository.TryConsumeAtomicAsync(request.TenantId, NormalizeKey(request.QuotaKey), request.Amount, now, ct);
        if (!result.Applied)
        {
            var existing = await _usageRepository.GetByTenantAndKeyAsync(request.TenantId, NormalizeKey(request.QuotaKey), ct);
            var error = existing is null ? QuotaErrorCodes.UsageNotFound : QuotaErrorCodes.LimitExceeded;
            await WriteEventAsync(request.TenantId, request.QuotaKey, request.Amount, request.Source, ReasonOrDefault(request.Reason, "Rejected quota consume."), request.OperationId, request.SourceReference, true, error, ct);
            return Response<QuotaMutationDto>.Fail(error, existing is null ? 404 : 409);
        }

        var usage = await SyncNotificationStateAsync(result.Usage!, request.Source, request.Reason, request.OperationId, request.SourceReference, ct);
        await WriteEventAsync(request.TenantId, request.QuotaKey, request.Amount, request.Source, ReasonOrDefault(request.Reason, "Quota consume."), request.OperationId, request.SourceReference, false, null, ct);
        return Response<QuotaMutationDto>.Success(ToMutation(usage, request.Amount, true, null));
    }

    public async Task<Response<QuotaMutationDto>> ReleaseAsync(ReleaseQuotaRequest request, CancellationToken ct)
    {
        var validation = ValidateMutationRequest(request.TenantId, request.QuotaKey, request.Amount, false);
        if (validation is not null)
        {
            return Response<QuotaMutationDto>.Fail(validation, 400);
        }

        if (await IsDuplicateAsync(request.TenantId, request.QuotaKey, request.Source, request.OperationId, request.SourceReference, false, ct))
        {
            return Response<QuotaMutationDto>.Fail(QuotaErrorCodes.DuplicateOperation, 409);
        }

        var now = DateTimeOffset.UtcNow;
        var result = await _usageRepository.TryReleaseAtomicAsync(request.TenantId, NormalizeKey(request.QuotaKey), request.Amount, now, ct);
        if (!result.Applied)
        {
            var existing = await _usageRepository.GetByTenantAndKeyAsync(request.TenantId, NormalizeKey(request.QuotaKey), ct);
            var error = existing is null ? QuotaErrorCodes.UsageNotFound : QuotaErrorCodes.ReleaseExceedsCurrentUsage;
            await WriteEventAsync(request.TenantId, request.QuotaKey, -request.Amount, request.Source, ReasonOrDefault(request.Reason, "Rejected quota release."), request.OperationId, request.SourceReference, true, error, ct);
            return Response<QuotaMutationDto>.Fail(error, existing is null ? 404 : 400);
        }

        await WriteEventAsync(request.TenantId, request.QuotaKey, -request.Amount, request.Source, ReasonOrDefault(request.Reason, "Quota release."), request.OperationId, request.SourceReference, false, null, ct);
        return Response<QuotaMutationDto>.Success(ToMutation(result.Usage!, -request.Amount, true, null));
    }

    public async Task<Response<QuotaStatusDto>> ResetPeriodAsync(ResetQuotaPeriodRequest request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.TenantRequired, 400);
        }

        if (!QuotaKeys.IsKnown(request.QuotaKey))
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.KeyUnknown, 400);
        }

        if (!QuotaKeys.IsResettable(request.QuotaKey) || request.PeriodEnd <= request.PeriodStart)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.PeriodResetNotAllowed, 400);
        }

        var usage = await _usageRepository.ResetPeriodAsync(request.TenantId, NormalizeKey(request.QuotaKey), request.PeriodStart, request.PeriodEnd, DateTimeOffset.UtcNow, ct);
        if (usage is null)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.UsageNotFound, 404);
        }

        await WriteEventAsync(request.TenantId, request.QuotaKey, 0, request.Source, ReasonOrDefault(request.Reason, "Quota period reset."), request.OperationId, request.SourceReference, false, null, ct);
        return Response<QuotaStatusDto>.Success(ToStatus(usage));
    }

    public async Task<Response<QuotaStatusDto>> RecalculateAsync(RecalculateQuotaUsageRequest request, CancellationToken ct)
    {
        if (request.TenantId == Guid.Empty)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.TenantRequired, 400);
        }

        if (!QuotaKeys.IsKnown(request.QuotaKey))
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.KeyUnknown, 400);
        }

        var usage = await _usageRepository.GetByTenantAndKeyAsync(request.TenantId, NormalizeKey(request.QuotaKey), ct);
        if (usage is null)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.UsageNotFound, 404);
        }

        var computed = await ComputeCurrentUsageAsync(request.TenantId, NormalizeKey(request.QuotaKey), ct);
        if (computed is null)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.RecalculationNotSupported, 400);
        }

        var delta = computed.Value - usage.CurrentValue;
        var updated = delta == 0
            ? usage
            : await _usageRepository.SetCurrentValueAsync(request.TenantId, NormalizeKey(request.QuotaKey), computed.Value, DateTimeOffset.UtcNow, ct);

        if (updated is null)
        {
            return Response<QuotaStatusDto>.Fail(QuotaErrorCodes.UsageNotFound, 404);
        }

        await WriteEventAsync(request.TenantId, request.QuotaKey, delta, request.Source, ReasonOrDefault(request.Reason, "Quota usage recalculated."), request.OperationId, request.SourceReference, false, null, ct);
        return Response<QuotaStatusDto>.Success(ToStatus(updated));
    }

    private async Task<ResolvedQuotaLimits> ResolveLimitsAsync(Guid tenantId, bool requireEligibleForMutation, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
        {
            return ResolvedQuotaLimits.Fail(QuotaErrorCodes.TenantRequired, 400);
        }

        var subscription = await _subscriptionRepository.GetCurrentByTenantIdAsync(tenantId, ct);
        if (subscription is null)
        {
            return ResolvedQuotaLimits.Fail(QuotaErrorCodes.ConfigurationMissing, 400);
        }

        if (requireEligibleForMutation && !IsSubscriptionEligible(subscription))
        {
            return ResolvedQuotaLimits.Fail(QuotaErrorCodes.SubscriptionInactive, 403);
        }

        var plan = await _planRepository.GetByIdAsync(subscription.PlanId, ct);
        if (plan?.DefaultQuotas is null || plan.DefaultQuotas.Count == 0)
        {
            return ResolvedQuotaLimits.Fail(QuotaErrorCodes.ConfigurationMissing, 400);
        }

        return ResolvedQuotaLimits.Success(subscription, plan, plan.DefaultQuotas);
    }

    private static bool IsSubscriptionEligible(TenantSubscription subscription)
    {
        if (subscription.Status == TenantSubscriptionStatus.Active)
        {
            return true;
        }

        if (subscription.Status == TenantSubscriptionStatus.Trialing)
        {
            return !subscription.TrialEndDateUtc.HasValue || subscription.TrialEndDateUtc.Value >= DateTimeOffset.UtcNow;
        }

        return false;
    }

    private async Task<decimal?> ComputeCurrentUsageAsync(Guid tenantId, string quotaKey, CancellationToken ct)
    {
        if (string.Equals(quotaKey, QuotaKeys.ModulesMax, StringComparison.OrdinalIgnoreCase))
        {
            var entitlements = await _entitlementRepository.GetByTenantIdAsync(tenantId, ct);
            return entitlements.Count(x => x.IsEnabled);
        }

        var tenant = await _tenantRepository.GetByIdAsync(tenantId, ct);
        if (tenant is null)
        {
            return null;
        }

        if (string.Equals(quotaKey, QuotaKeys.UsersMax, StringComparison.OrdinalIgnoreCase))
        {
            return tenant.AdminUsers.Count(user => user.Status is TenantAdminUserStatus.Invited or TenantAdminUserStatus.Active);
        }

        if (string.Equals(quotaKey, QuotaKeys.StorageGbMax, StringComparison.OrdinalIgnoreCase))
        {
            return tenant.StorageUsedGb;
        }

        return null;
    }

    private async Task<QuotaUsage> SyncNotificationStateAsync(QuotaUsage usage, string source, string? reason, string? operationId, string? sourceReference, CancellationToken ct)
    {
        var usagePercent = CalculateUsagePercent(usage.CurrentValue, usage.LimitValue);
        var warningDue = usagePercent >= 80 && !usage.WarningNotificationSentForPeriod;
        var breachDue = usagePercent >= 100 && !usage.LimitBreachNotificationSentForPeriod;
        if (!warningDue && !breachDue)
        {
            return usage;
        }

        var now = DateTimeOffset.UtcNow;
        var updated = await _usageRepository.MarkNotificationStateAsync(usage.TenantId, usage.QuotaKey, warningDue, breachDue, now, ct) ?? usage;

        if (warningDue)
        {
            await WriteEventAsync(usage.TenantId, usage.QuotaKey, 0, "QuotaWarningNotification", ReasonOrDefault(reason, "Quota warning threshold reached."), operationId, sourceReference, false, null, ct);
        }

        if (breachDue)
        {
            await WriteEventAsync(usage.TenantId, usage.QuotaKey, 0, "QuotaLimitBreachNotification", ReasonOrDefault(reason, "Quota hard limit reached."), operationId, sourceReference, false, null, ct);
        }

        _logger.LogInformation(
            "Quota notification seam emitted. TenantId={TenantId} QuotaKey={QuotaKey} Source={Source} Warning={Warning} Breach={Breach}",
            usage.TenantId,
            usage.QuotaKey,
            source,
            warningDue,
            breachDue);

        return updated;
    }

    private async Task WriteEventAsync(Guid tenantId, string quotaKey, decimal delta, string source, string reason, string? operationId, string? sourceReference, bool isRejected, string? errorCode, CancellationToken ct)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(quotaKey) || string.IsNullOrWhiteSpace(source))
        {
            return;
        }

        await _eventRepository.CreateAsync(new QuotaEvent
        {
            TenantId = tenantId,
            QuotaKey = NormalizeKey(quotaKey),
            Delta = delta,
            Source = source.Trim(),
            Reason = reason,
            OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim(),
            SourceReference = string.IsNullOrWhiteSpace(sourceReference) ? null : sourceReference.Trim(),
            IsRejected = isRejected,
            ErrorCode = errorCode
        }, ct);
    }

    private async Task<bool> IsDuplicateAsync(Guid tenantId, string quotaKey, string source, string? operationId, string? sourceReference, bool isRejected, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId) && string.IsNullOrWhiteSpace(sourceReference))
        {
            return false;
        }

        return await _eventRepository.ExistsAsync(tenantId, NormalizeKey(quotaKey), source, operationId, sourceReference, isRejected, ct);
    }

    private static string? ValidateMutationRequest(Guid tenantId, string quotaKey, decimal amount, bool consume)
    {
        if (tenantId == Guid.Empty)
        {
            return QuotaErrorCodes.TenantRequired;
        }

        if (!QuotaKeys.IsKnown(quotaKey))
        {
            return QuotaErrorCodes.KeyUnknown;
        }

        if (amount <= 0)
        {
            return consume ? QuotaErrorCodes.LimitExceeded : QuotaErrorCodes.ReleaseInvalidAmount;
        }

        return null;
    }

    private static QuotaStatusDto ToStatus(QuotaUsage usage)
    {
        var usagePercent = CalculateUsagePercent(usage.CurrentValue, usage.LimitValue);
        return new QuotaStatusDto(
            usage.TenantId,
            usage.QuotaKey,
            usage.CurrentValue,
            usage.LimitValue,
            usagePercent,
            usagePercent >= 80,
            usage.CurrentValue >= usage.LimitValue,
            usage.PeriodStart,
            usage.PeriodEnd,
            usage.Source,
            usage.SubscriptionId,
            usage.PlanId,
            usage.OverrideSource,
            usage.WarningNotificationSentForPeriod,
            usage.LimitBreachNotificationSentForPeriod,
            usage.LastWarningNotifiedAtUtc,
            usage.LastLimitBreachNotifiedAtUtc);
    }

    private static QuotaMutationDto ToMutation(QuotaUsage usage, decimal delta, bool applied, string? errorCode) =>
        new(usage.TenantId, usage.QuotaKey, usage.CurrentValue, usage.LimitValue, delta, applied, errorCode);

    private static decimal CalculateUsagePercent(decimal currentValue, decimal limitValue)
    {
        if (limitValue <= 0)
        {
            return currentValue > 0 ? 100 : 0;
        }

        return Math.Round(currentValue / limitValue * 100, 2);
    }

    private static string NormalizeKey(string quotaKey) => quotaKey.Trim().ToLowerInvariant();

    private static string ReasonOrDefault(string? reason, string fallback) =>
        string.IsNullOrWhiteSpace(reason) ? fallback : reason.Trim();

    private sealed record ResolvedQuotaLimits(
        bool IsSuccessful,
        string? ErrorCode,
        int StatusCode,
        TenantSubscription? Subscription,
        SubscriptionPlan? Plan,
        IReadOnlyDictionary<string, decimal>? Quotas)
    {
        public static ResolvedQuotaLimits Success(TenantSubscription subscription, SubscriptionPlan plan, IReadOnlyDictionary<string, decimal> quotas) =>
            new(true, null, 200, subscription, plan, quotas);

        public static ResolvedQuotaLimits Fail(string errorCode, int statusCode) =>
            new(false, errorCode, statusCode, null, null, null);
    }
}
