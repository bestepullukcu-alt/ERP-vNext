using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.Tenants;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;

namespace Diten.Platform.Application.Features.Audit;

public class AuditEventFilterRequest
{
    public Guid? TenantId { get; set; }
    public Guid? TargetTenantId { get; set; }
    public Guid? CorrelationId { get; set; }
    public Guid? ActorId { get; set; }
    public string? Category { get; set; }
    public string? Operation { get; set; }
    public string? Outcome { get; set; }
    public string? EntityType { get; set; }
    public Guid? EntityId { get; set; }
    public string? SourceModule { get; set; }
    public DateTimeOffset? FromUtc { get; set; }
    public DateTimeOffset? ToUtc { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public sealed class AuditExportRequest : AuditEventFilterRequest
{
    public string Format { get; set; } = AuditExportFormats.Csv;
    public int Limit { get; set; } = AuditExportLimits.MaxRows;
}

public sealed record AuditEventListItemDto(
    Guid Id,
    Guid TenantId,
    Guid? TargetTenantId,
    Guid CorrelationId,
    string ActorType,
    Guid? ActorId,
    string? ActorEmailMasked,
    string? ActorDisplayNameMasked,
    string Category,
    string EntityType,
    Guid? EntityId,
    string Operation,
    string Outcome,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset WrittenAtUtc,
    string SourceService,
    string? SourceModule,
    bool IsMetaAudit,
    string RedactionStatus);

public sealed record AuditEventDetailDto(
    Guid Id,
    Guid TenantId,
    Guid? TargetTenantId,
    Guid CorrelationId,
    string? RequestType,
    string ActorType,
    Guid? ActorId,
    string? ActorEmailMasked,
    string? ActorDisplayNameMasked,
    string Category,
    string EntityType,
    Guid? EntityId,
    string Operation,
    string Outcome,
    IReadOnlyDictionary<string, object?>? BeforeState,
    IReadOnlyDictionary<string, object?>? AfterState,
    IReadOnlyDictionary<string, object?> Metadata,
    string? IpAddressMasked,
    string? UserAgent,
    DateTimeOffset OccurredAtUtc,
    DateTimeOffset WrittenAtUtc,
    string SourceService,
    string? SourceModule,
    bool IsMetaAudit,
    string RedactionStatus,
    DateTimeOffset? RedactedAtUtc,
    Guid? RedactedByActorId,
    string? RedactionReason);

public sealed record AuditExportResultDto(
    byte[] Content,
    string ContentType,
    string FileName,
    int RowCount);

public sealed class UpdateAuditRetentionRequest
{
    public Guid PolicyId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string PlanTierCode { get; set; } = AuditEventRetentionPolicy.DefaultPlanTierCode;
    public int MinimumRetentionDays { get; set; }
    public int DefaultRetentionDays { get; set; }
    public int MaximumRetentionDays { get; set; }
    public int HotStorageDays { get; set; }
    public bool ColdStoragePrepared { get; set; } = true;
    public bool AllowTenantOverride { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record AuditRetentionPolicyDto(
    Guid Id,
    string Category,
    string PlanTierCode,
    int MinimumRetentionDays,
    int DefaultRetentionDays,
    int MaximumRetentionDays,
    int HotStorageDays,
    bool ColdStoragePrepared,
    bool AllowTenantOverride,
    bool IsActive);

public sealed class RedactAuditActorRequest
{
    public Guid ActorId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public sealed record RedactAuditActorResultDto(
    Guid ActorId,
    int RedactedEventCount,
    DateTimeOffset RedactedAtUtc);

public sealed record AuditExportLimitsDto(int MaxRows, int MaxDays);

public static class AuditExportFormats
{
    public const string Csv = "csv";
    public const string Json = "json";
}

public static class AuditExportLimits
{
    public const int MaxRows = 50_000;
    public const int MaxDays = 365;
}

public static class AuditEventMapper
{
    public static AuditEventListItemDto ToListDto(AuditEvent auditEvent) =>
        new(
            auditEvent.Id,
            auditEvent.TenantId,
            auditEvent.TargetTenantId,
            auditEvent.CorrelationId,
            auditEvent.ActorType.ToString(),
            auditEvent.ActorId,
            auditEvent.ActorEmailMasked,
            auditEvent.ActorDisplayNameMasked,
            auditEvent.Category.ToString(),
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.Operation.ToString(),
            auditEvent.Outcome.ToString(),
            auditEvent.OccurredAtUtc,
            auditEvent.WrittenAtUtc,
            auditEvent.SourceService,
            auditEvent.SourceModule,
            auditEvent.IsMetaAudit,
            auditEvent.RedactionStatus.ToString());

    public static AuditEventDetailDto ToDetailDto(AuditEvent auditEvent, ISensitiveFieldRedactor redactor) =>
        new(
            auditEvent.Id,
            auditEvent.TenantId,
            auditEvent.TargetTenantId,
            auditEvent.CorrelationId,
            auditEvent.RequestType,
            auditEvent.ActorType.ToString(),
            auditEvent.ActorId,
            auditEvent.ActorEmailMasked,
            auditEvent.ActorDisplayNameMasked,
            auditEvent.Category.ToString(),
            auditEvent.EntityType,
            auditEvent.EntityId,
            auditEvent.Operation.ToString(),
            auditEvent.Outcome.ToString(),
            RedactNullableDictionary(auditEvent.BeforeState, redactor),
            RedactNullableDictionary(auditEvent.AfterState, redactor),
            redactor.RedactDictionary(auditEvent.Metadata),
            auditEvent.IpAddressMasked,
            auditEvent.UserAgent,
            auditEvent.OccurredAtUtc,
            auditEvent.WrittenAtUtc,
            auditEvent.SourceService,
            auditEvent.SourceModule,
            auditEvent.IsMetaAudit,
            auditEvent.RedactionStatus.ToString(),
            auditEvent.RedactedAtUtc,
            auditEvent.RedactedByActorId,
            auditEvent.RedactionReason);

    public static PagedResult<AuditEventListItemDto> ToPagedResult(
        IReadOnlyList<AuditEvent> items,
        int page,
        int pageSize,
        long totalCount)
    {
        var normalizedPageSize = Math.Clamp(pageSize, 1, 500);
        var totalPages = totalCount == 0 ? 0 : (int)Math.Ceiling(totalCount / (double)normalizedPageSize);
        return new PagedResult<AuditEventListItemDto>(
            items.Select(ToListDto).ToList(),
            Math.Max(page, 1),
            normalizedPageSize,
            totalCount,
            totalPages);
    }

    public static AuditRetentionPolicyDto ToRetentionDto(AuditEventRetentionPolicy policy) =>
        new(
            policy.Id,
            policy.Category.ToString(),
            policy.PlanTierCode,
            policy.MinimumRetentionDays,
            policy.DefaultRetentionDays,
            policy.MaximumRetentionDays,
            policy.HotStorageDays,
            policy.ColdStoragePrepared,
            policy.AllowTenantOverride,
            policy.IsActive);

    private static IReadOnlyDictionary<string, object?>? RedactNullableDictionary(
        IReadOnlyDictionary<string, object?>? value,
        ISensitiveFieldRedactor redactor)
    {
        return value is null ? null : redactor.RedactDictionary(value);
    }
}

public static class AuditFilterParser
{
    public static bool TryBuildListSearchRequest(
        AuditEventFilterRequest filter,
        out AuditEventSearchRequest request,
        out string? error)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);
        return TryBuildSearchRequest(filter, (page - 1) * pageSize, pageSize, out request, out error);
    }

    public static bool TryBuildExportSearchRequest(
        AuditExportRequest filter,
        out AuditEventSearchRequest request,
        out string? error)
    {
        if (filter.Limit is < 1 or > AuditExportLimits.MaxRows)
        {
            request = EmptySearchRequest();
            error = $"Export limit must be between 1 and {AuditExportLimits.MaxRows}.";
            return false;
        }

        if (!filter.FromUtc.HasValue || !filter.ToUtc.HasValue)
        {
            request = EmptySearchRequest();
            error = "Export requires FromUtc and ToUtc.";
            return false;
        }

        var range = filter.ToUtc.Value - filter.FromUtc.Value;
        if (range.TotalDays > AuditExportLimits.MaxDays)
        {
            request = EmptySearchRequest();
            error = $"Export date range cannot exceed {AuditExportLimits.MaxDays} days.";
            return false;
        }

        return TryBuildSearchRequest(filter, 0, filter.Limit, out request, out error);
    }

    public static bool TryParseCategory(string? raw, out AuditCategory? category, out string? error) =>
        TryParseEnum(raw, "Category", out category, out error);

    public static bool TryParseExportFormat(string? raw, out string format, out string? error)
    {
        format = string.IsNullOrWhiteSpace(raw) ? AuditExportFormats.Csv : raw.Trim().ToLowerInvariant();
        if (format is AuditExportFormats.Csv or AuditExportFormats.Json)
        {
            error = null;
            return true;
        }

        error = "Export format must be csv or json.";
        return false;
    }

    private static bool TryBuildSearchRequest(
        AuditEventFilterRequest filter,
        int skip,
        int take,
        out AuditEventSearchRequest request,
        out string? error)
    {
        request = EmptySearchRequest();

        if (filter.FromUtc.HasValue && filter.ToUtc.HasValue && filter.FromUtc.Value > filter.ToUtc.Value)
        {
            error = "FromUtc must be before or equal to ToUtc.";
            return false;
        }

        if (filter.EntityType is { Length: > 160 })
        {
            error = "EntityType cannot exceed 160 characters.";
            return false;
        }

        if (filter.SourceModule is { Length: > 120 })
        {
            error = "SourceModule cannot exceed 120 characters.";
            return false;
        }

        if (!TryParseEnum(filter.Category, "Category", out AuditCategory? category, out error)
            || !TryParseEnum(filter.Operation, "Operation", out AuditOperation? operation, out error)
            || !TryParseEnum(filter.Outcome, "Outcome", out AuditOutcome? outcome, out error))
        {
            return false;
        }

        request = new AuditEventSearchRequest(
            filter.TenantId,
            filter.TargetTenantId,
            filter.CorrelationId,
            filter.ActorId,
            category,
            operation,
            outcome,
            NormalizeOptional(filter.EntityType),
            filter.EntityId,
            NormalizeOptional(filter.SourceModule),
            filter.FromUtc,
            filter.ToUtc,
            Math.Max(skip, 0),
            Math.Clamp(take, 1, AuditExportLimits.MaxRows));
        error = null;
        return true;
    }

    private static bool TryParseEnum<TEnum>(string? raw, string fieldName, out TEnum? value, out string? error)
        where TEnum : struct, Enum
    {
        value = null;
        error = null;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (Enum.TryParse<TEnum>(raw.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed)
            && !EqualityComparer<TEnum>.Default.Equals(parsed, default))
        {
            value = parsed;
            return true;
        }

        error = $"{fieldName} contains an invalid value.";
        return false;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static AuditEventSearchRequest EmptySearchRequest() =>
        new(null, null, null, null, null, null, null, null, null, null, null, null, 0, 1);
}
