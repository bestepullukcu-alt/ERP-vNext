namespace Diten.Platform.Application.Features.Quotas;

public sealed record QuotaStatusDto(
    Guid TenantId,
    string QuotaKey,
    decimal CurrentValue,
    decimal LimitValue,
    decimal UsagePercent,
    bool IsWarning,
    bool IsLimitExceeded,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    string Source,
    Guid? SubscriptionId,
    Guid? PlanId,
    string? OverrideSource,
    bool WarningNotificationSentForPeriod,
    bool LimitBreachNotificationSentForPeriod,
    DateTimeOffset? LastWarningNotifiedAtUtc,
    DateTimeOffset? LastLimitBreachNotifiedAtUtc);

public sealed record QuotaMutationDto(
    Guid TenantId,
    string QuotaKey,
    decimal CurrentValue,
    decimal LimitValue,
    decimal Delta,
    bool Applied,
    string? ErrorCode);

public sealed record InitializeTenantQuotasRequest(
    string? Reason,
    string? Source,
    string? ActorId,
    string? CorrelationId);

public sealed record SyncTenantQuotaLimitsRequest(
    string? Reason,
    string? Source,
    string? ActorId,
    string? CorrelationId);

public sealed record TryConsumeQuotaRequest(
    Guid TenantId,
    string QuotaKey,
    decimal Amount,
    string Source,
    string? OperationId,
    string? SourceReference,
    string? Reason,
    string? ActorId,
    string? CorrelationId);

public sealed record ReleaseQuotaRequest(
    Guid TenantId,
    string QuotaKey,
    decimal Amount,
    string Source,
    string? OperationId,
    string? SourceReference,
    string? Reason,
    string? ActorId,
    string? CorrelationId);

public sealed record ResetQuotaPeriodRequest(
    Guid TenantId,
    string QuotaKey,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    string Source,
    string? OperationId,
    string? SourceReference,
    string? Reason,
    string? ActorId,
    string? CorrelationId);

public sealed record RecalculateQuotaUsageRequest(
    Guid TenantId,
    string QuotaKey,
    string Source,
    string? OperationId,
    string? SourceReference,
    string? Reason,
    string? ActorId,
    string? CorrelationId);

public static class QuotaKeys
{
    public const string UsersMax = "users.max";
    public const string StorageGbMax = "storage.gb.max";
    public const string ApiCallsPerMonth = "api.calls.per.month";
    public const string ModulesMax = "modules.max";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        UsersMax,
        StorageGbMax,
        ApiCallsPerMonth,
        ModulesMax
    };

    public static bool IsKnown(string quotaKey) => All.Contains(quotaKey);

    public static bool IsResettable(string quotaKey) =>
        string.Equals(quotaKey, ApiCallsPerMonth, StringComparison.OrdinalIgnoreCase);
}

public static class QuotaErrorCodes
{
    public const string UsageNotFound = "QUOTA_USAGE_NOT_FOUND";
    public const string KeyUnknown = "QUOTA_KEY_UNKNOWN";
    public const string LimitExceeded = "QUOTA_LIMIT_EXCEEDED";
    public const string ConfigurationMissing = "QUOTA_CONFIGURATION_MISSING";
    public const string TenantRequired = "QUOTA_TENANT_REQUIRED";
    public const string ConcurrencyConflict = "QUOTA_CONCURRENCY_CONFLICT";
    public const string ReleaseInvalidAmount = "QUOTA_RELEASE_INVALID_AMOUNT";
    public const string PeriodResetNotAllowed = "QUOTA_PERIOD_RESET_NOT_ALLOWED";
    public const string SubscriptionInactive = "QUOTA_SUBSCRIPTION_INACTIVE";
    public const string InitializationFailed = "QUOTA_INITIALIZATION_FAILED";
    public const string RecalculationNotSupported = "QUOTA_RECALCULATION_NOT_SUPPORTED";
    public const string OverrideReasonRequired = "QUOTA_OVERRIDE_REASON_REQUIRED";
    public const string LimitSyncRequired = "QUOTA_LIMIT_SYNC_REQUIRED";
    public const string ReleaseExceedsCurrentUsage = "QUOTA_RELEASE_EXCEEDS_CURRENT_USAGE";
    public const string DuplicateOperation = "QUOTA_DUPLICATE_OPERATION";
    public const string OperationReferenceRequired = "QUOTA_OPERATION_REFERENCE_REQUIRED";
}
