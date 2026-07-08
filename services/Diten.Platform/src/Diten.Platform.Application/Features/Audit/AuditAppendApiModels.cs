using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;

namespace Diten.Platform.Application.Features.Audit;

public sealed record GovernedAuditAppendRequest
{
    public Guid CorrelationId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public string ActorType { get; init; } = "TenantUser";
    public Guid? ActorId { get; init; }
    public Guid? TargetTenantId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string Operation { get; init; } = string.Empty;
    public string Outcome { get; init; } = "Succeeded";
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
    public DateTimeOffset? OccurredAtUtc { get; init; }
    public string SourceService { get; init; } = string.Empty;
    public string? SourceModule { get; init; }
    public int Sequence { get; init; }
}

public sealed record GovernedAuditAppendResponse(
    string Status,
    string? IdempotencyKey,
    bool AuthoritativePersistenceAccepted,
    bool Duplicate,
    bool ShouldBlockBusinessCommand);

public static class GovernedAuditAppendValidation
{
    private static readonly IReadOnlySet<string> ProhibitedMetadataKeyMarkers = new HashSet<string>(
        [
            "password",
            "pwd",
            "secret",
            "token",
            "authorization",
            "apikey",
            "api_key",
            "connection_string",
            "connectionstring",
            "government_identifier",
            "national_id",
            "ssn",
            "tax_id",
            "passport",
            "before",
            "after",
            "old_value",
            "new_value"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<string> Validate(
        GovernedAuditAppendRequest request,
        Guid resolvedTenantId)
    {
        var errors = new List<string>();

        if (request.CorrelationId == Guid.Empty)
        {
            errors.Add("correlation_id_required");
        }

        if (string.IsNullOrWhiteSpace(request.RequestType))
        {
            errors.Add("request_type_required");
        }

        if (string.IsNullOrWhiteSpace(request.Category))
        {
            errors.Add("category_required");
        }

        if (string.IsNullOrWhiteSpace(request.EntityType))
        {
            errors.Add("entity_type_required");
        }

        if (string.IsNullOrWhiteSpace(request.Operation))
        {
            errors.Add("operation_required");
        }

        if (string.IsNullOrWhiteSpace(request.SourceService))
        {
            errors.Add("source_service_required");
        }

        if (request.TargetTenantId.HasValue && request.TargetTenantId.Value != resolvedTenantId)
        {
            errors.Add("target_tenant_mismatch");
        }

        if (!TryParseActorType(request.ActorType, out _))
        {
            errors.Add("actor_type_invalid");
        }

        if (!TryParseCategory(request.Category, out _))
        {
            errors.Add("category_invalid");
        }

        if (!TryParseOperation(request.Operation, out _))
        {
            errors.Add("operation_invalid");
        }

        if (!TryParseOutcome(request.Outcome, out _))
        {
            errors.Add("outcome_invalid");
        }

        if (!HasSafeMetadataKeys(request.Metadata))
        {
            errors.Add("metadata_contains_prohibited_key");
        }

        return errors;
    }

    public static bool HasSafeMetadataKeys(IReadOnlyDictionary<string, object?> metadata)
        => metadata.Keys.All(IsSafeMetadataKey);

    public static bool TryParseActorType(string? value, out AuditActorType parsed)
        => Enum.TryParse(NormalizeEnumValue(value), ignoreCase: true, out parsed)
           && parsed != AuditActorType.Unknown;

    public static bool TryParseCategory(string? value, out AuditCategory parsed)
        => Enum.TryParse(NormalizeEnumValue(value), ignoreCase: true, out parsed)
           && parsed != AuditCategory.Unknown;

    public static bool TryParseOperation(string? value, out AuditOperation parsed)
        => Enum.TryParse(NormalizeEnumValue(value), ignoreCase: true, out parsed)
           && parsed != AuditOperation.Unknown;

    public static bool TryParseOutcome(string? value, out AuditOutcome parsed)
        => Enum.TryParse(NormalizeEnumValue(value), ignoreCase: true, out parsed)
           && parsed != AuditOutcome.Unknown;

    public static GovernedAuditAppendResponse ToResponse(AuditAppendResult result)
        => new(
            result.Status.ToString(),
            result.IdempotencyKey,
            result.IsEnqueued || result.IsDuplicate,
            result.IsDuplicate,
            result.ShouldBreakBusinessCommand || result.Status == AuditAppendStatus.EnqueueFailed);

    private static bool IsSafeMetadataKey(string key)
    {
        var normalized = key
            .Replace("-", "_", StringComparison.Ordinal)
            .Replace(".", "_", StringComparison.Ordinal)
            .Trim();

        return !ProhibitedMetadataKeyMarkers.Any(marker =>
            normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeEnumValue(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Replace("_", string.Empty, StringComparison.Ordinal)
                .Replace("-", string.Empty, StringComparison.Ordinal)
                .Trim();
}
