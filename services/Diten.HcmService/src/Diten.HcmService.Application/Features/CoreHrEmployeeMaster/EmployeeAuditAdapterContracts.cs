namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public interface IHcmAuditAppendClient
{
    Task<HcmAuditAppendResult> AppendAsync(HcmAuditAppendRequest request, CancellationToken cancellationToken);
}

public sealed record HcmAuditAppendResult(
    bool AuthoritativePersistenceAccepted,
    bool ShouldBlockActivationGradeOperation,
    int? HttpStatusCode,
    string ReasonCode,
    string? ProviderStatus = null)
{
    public bool AllowsActivationGradeSuccess =>
        AuthoritativePersistenceAccepted && !ShouldBlockActivationGradeOperation;

    public static HcmAuditAppendResult Accepted(int httpStatusCode, string? providerStatus = null)
        => new(true, false, httpStatusCode, "authoritative_persistence_accepted", providerStatus);

    public static HcmAuditAppendResult Blocked(string reasonCode, int? httpStatusCode = null, string? providerStatus = null)
        => new(false, true, httpStatusCode, reasonCode, providerStatus);
}

public sealed record HcmAuditAppendRequest
{
    public Guid CorrelationId { get; init; }
    public string RequestType { get; init; } = string.Empty;
    public string ActorType { get; init; } = "TenantUser";
    public Guid? ActorId { get; init; }
    public Guid TargetTenantId { get; init; }
    public string Category { get; init; } = "System";
    public string EntityType { get; init; } = string.Empty;
    public Guid? EntityId { get; init; }
    public string Operation { get; init; } = "Execute";
    public string Outcome { get; init; } = "Succeeded";
    public IReadOnlyDictionary<string, object?> Metadata { get; init; } = new Dictionary<string, object?>();
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public string SourceService { get; init; } = "Diten.HcmService";
    public string SourceModule { get; init; } = "MOD-0251";
    public bool IsAuthoritativePersistence { get; init; }
}

public static class EmployeeAuditAdapterMapper
{
    private static readonly IReadOnlySet<string> ProhibitedMetadataKeys = new HashSet<string>(
        [
            "legal_first_name",
            "legal_middle_name",
            "legal_last_name",
            "preferred_name",
            "date_of_birth",
            "dob",
            "email",
            "work_email",
            "personal_email",
            "phone",
            "government_identifier",
            "government_identifier_token",
            "government_identifier_hash",
            "national_id",
            "ssn",
            "tax_id",
            "passport_number",
            "before",
            "after",
            "old_value",
            "new_value",
            "reason_note",
            "secret",
            "token",
            "connection_string"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static HcmAuditAppendRequest MapDraftEvent(DraftAuditEvent auditEvent, DateTimeOffset? occurredAtUtc = null)
    {
        var metadata = ToObjectMetadata(auditEvent.Metadata);
        metadata["idempotency_key_hash"] = auditEvent.IdempotencyKeyHash;
        EnsureSafeMetadata(metadata);

        var correlationId = TryParseGuid(auditEvent.CorrelationId) ?? Guid.NewGuid();
        var actorId = TryParseGuid(auditEvent.ActorId);

        return new HcmAuditAppendRequest
        {
            CorrelationId = correlationId,
            RequestType = auditEvent.EventName,
            ActorType = actorId.HasValue ? "TenantUser" : "System",
            ActorId = actorId,
            TargetTenantId = auditEvent.TenantId,
            Category = "System",
            EntityType = "EmployeeDraftSession",
            EntityId = auditEvent.DraftSessionId,
            Operation = MapOperation(auditEvent.EventName),
            Outcome = "Succeeded",
            Metadata = metadata,
            OccurredAtUtc = occurredAtUtc ?? DateTimeOffset.UtcNow,
            IsAuthoritativePersistence = false
        };
    }

    public static HcmAuditAppendRequest MapEmployeeEvent(EmployeeAuditPayload payload, DateTimeOffset? occurredAtUtc = null)
    {
        var metadata = ToObjectMetadata(payload.Metadata);
        if (!string.IsNullOrWhiteSpace(payload.IdempotencyKeyHash))
        {
            metadata["idempotency_key_hash"] = payload.IdempotencyKeyHash;
        }

        EnsureSafeMetadata(metadata);

        return new HcmAuditAppendRequest
        {
            CorrelationId = TryParseGuid(payload.CorrelationId) ?? Guid.NewGuid(),
            RequestType = payload.EventName,
            ActorType = "TenantUser",
            ActorId = payload.ActorId,
            TargetTenantId = payload.TenantId,
            Category = MapCategory(payload.EventName),
            EntityType = MapEntityType(payload),
            EntityId = payload.EmployeeId ?? payload.EmploymentRecordId ?? payload.StatusHistoryId,
            Operation = MapOperation(payload.EventName),
            Outcome = MapOutcome(payload.EventName),
            Metadata = metadata,
            OccurredAtUtc = occurredAtUtc ?? DateTimeOffset.UtcNow,
            IsAuthoritativePersistence = false
        };
    }

    public static bool IsSafeMetadata(IReadOnlyDictionary<string, object?> metadata)
        => metadata.Keys.All(IsSafeMetadataKey);

    private static string MapCategory(string eventName)
        => eventName switch
        {
            EmployeeAuditEventNames.AccessDenied => "Security",
            EmployeeAuditEventNames.ViewSensitive => "DataPrivacy",
            EmployeeAuditEventNames.RegistrySearched => "System",
            _ => "System"
        };

    private static string MapEntityType(EmployeeAuditPayload payload)
        => payload.EventName switch
        {
            EmployeeAuditEventNames.EmploymentRecordUpdated => "EmploymentRecord",
            EmployeeAuditEventNames.StatusChanged => "EmployeeStatusHistory",
            EmployeeAuditEventNames.AccessDenied => "AccessDenied",
            _ => "Employee"
        };

    private static string MapOperation(string eventName)
        => eventName switch
        {
            "employee_draft.created" => "Create",
            "employee_draft.updated" => "Update",
            "employee_draft.references_validated" => "Execute",
            "employee_draft.reviewed" => "Execute",
            EmployeeAuditEventNames.ProfileUpdated => "Update",
            EmployeeAuditEventNames.EmploymentRecordUpdated => "Update",
            EmployeeAuditEventNames.StatusChanged => "Update",
            EmployeeAuditEventNames.AccessDenied => "PermissionDenied",
            EmployeeAuditEventNames.ViewSensitive => "Execute",
            EmployeeAuditEventNames.RegistrySearched => "Execute",
            _ => "Execute"
        };

    private static string MapOutcome(string eventName)
        => eventName == EmployeeAuditEventNames.AccessDenied ? "Denied" : "Succeeded";

    private static Dictionary<string, object?> ToObjectMetadata(IReadOnlyDictionary<string, string> metadata)
        => metadata.ToDictionary(pair => pair.Key, pair => (object?)pair.Value, StringComparer.OrdinalIgnoreCase);

    private static void EnsureSafeMetadata(IReadOnlyDictionary<string, object?> metadata)
    {
        if (!IsSafeMetadata(metadata))
        {
            throw new ArgumentException("Audit metadata contains prohibited PII, secret, or before/after keys.", nameof(metadata));
        }
    }

    private static bool IsSafeMetadataKey(string key)
    {
        var normalized = key.Replace("-", "_", StringComparison.Ordinal).Trim();
        return !ProhibitedMetadataKeys.Any(marker => normalized.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? TryParseGuid(string? value)
        => Guid.TryParse(value, out var parsed) ? parsed : null;
}
