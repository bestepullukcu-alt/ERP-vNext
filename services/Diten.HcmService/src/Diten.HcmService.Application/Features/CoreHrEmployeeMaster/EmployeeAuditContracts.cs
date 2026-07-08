namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public static class EmployeeAuditEventNames
{
    public const string ProfileUpdated = "employee.profile.updated";
    public const string EmploymentRecordUpdated = "employee.employment_record.updated";
    public const string StatusChanged = "employee.status.changed";
    public const string AccessDenied = "employee.access_denied";
    public const string ViewSensitive = "employee.view_sensitive";
    public const string RegistrySearched = "employee.registry.searched";
}

public sealed record EmployeeAuditPayload(
    string EventName,
    Guid TenantId,
    Guid ActorId,
    Guid? EmployeeId,
    Guid? EmploymentRecordId,
    Guid? StatusHistoryId,
    string CorrelationId,
    string? IdempotencyKeyHash,
    IReadOnlyDictionary<string, string> Metadata);

public static class EmployeeAuditPayloadBuilder
{
    private static readonly IReadOnlySet<string> ProhibitedMetadataKeys = new HashSet<string>(
        [
            "legal_first_name",
            "legal_middle_name",
            "legal_last_name",
            "preferred_name",
            "date_of_birth",
            "government_identifier",
            "government_identifier_token",
            "work_email",
            "personal_email",
            "phone",
            "reason_note",
            "before",
            "after",
            "old_value",
            "new_value"
        ],
        StringComparer.OrdinalIgnoreCase);

    public static EmployeeAuditPayload ProfileUpdated(
        Guid tenantId,
        Guid actorId,
        Guid employeeId,
        string correlationId,
        string idempotencyKeyHash,
        IEnumerable<string> changedFieldNames,
        int version)
        => Build(
            EmployeeAuditEventNames.ProfileUpdated,
            tenantId,
            actorId,
            employeeId,
            null,
            null,
            correlationId,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["changed_fields"] = JoinSafeFieldNames(changedFieldNames),
                ["version"] = version.ToString()
            });

    public static EmployeeAuditPayload EmploymentRecordUpdated(
        Guid tenantId,
        Guid actorId,
        Guid employeeId,
        Guid employmentRecordId,
        string correlationId,
        string idempotencyKeyHash,
        IEnumerable<string> changedFieldNames,
        int version)
        => Build(
            EmployeeAuditEventNames.EmploymentRecordUpdated,
            tenantId,
            actorId,
            employeeId,
            employmentRecordId,
            null,
            correlationId,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["changed_fields"] = JoinSafeFieldNames(changedFieldNames),
                ["version"] = version.ToString()
            });

    public static EmployeeAuditPayload StatusChanged(
        Guid tenantId,
        Guid actorId,
        Guid employeeId,
        Guid statusHistoryId,
        string previousStatus,
        string newStatus,
        string correlationId,
        string idempotencyKeyHash,
        int version)
        => Build(
            EmployeeAuditEventNames.StatusChanged,
            tenantId,
            actorId,
            employeeId,
            null,
            statusHistoryId,
            correlationId,
            idempotencyKeyHash,
            new Dictionary<string, string>
            {
                ["previous_status"] = previousStatus,
                ["new_status"] = newStatus,
                ["version"] = version.ToString()
            });

    public static EmployeeAuditPayload AccessDenied(
        Guid tenantId,
        Guid actorId,
        string permission,
        string targetType,
        Guid? employeeId,
        string correlationId)
        => Build(
            EmployeeAuditEventNames.AccessDenied,
            tenantId,
            actorId,
            employeeId,
            null,
            null,
            correlationId,
            null,
            new Dictionary<string, string>
            {
                ["permission"] = permission,
                ["target_type"] = targetType
            });

    public static EmployeeAuditPayload ViewSensitive(
        Guid tenantId,
        Guid actorId,
        Guid employeeId,
        string correlationId,
        IEnumerable<string> returnedSensitivityClasses)
        => Build(
            EmployeeAuditEventNames.ViewSensitive,
            tenantId,
            actorId,
            employeeId,
            null,
            null,
            correlationId,
            null,
            new Dictionary<string, string>
            {
                ["returned_sensitivity_classes"] = JoinSafeFieldNames(returnedSensitivityClasses)
            });

    public static EmployeeAuditPayload RegistrySearched(
        Guid tenantId,
        Guid actorId,
        string correlationId,
        string filterHash,
        int rowLimit)
        => Build(
            EmployeeAuditEventNames.RegistrySearched,
            tenantId,
            actorId,
            null,
            null,
            null,
            correlationId,
            null,
            new Dictionary<string, string>
            {
                ["filter_hash"] = filterHash,
                ["row_limit"] = rowLimit.ToString()
            });

    public static bool IsSafeMetadata(IReadOnlyDictionary<string, string> metadata)
        => metadata.Keys.All(key => !ProhibitedMetadataKeys.Contains(key));

    private static EmployeeAuditPayload Build(
        string eventName,
        Guid tenantId,
        Guid actorId,
        Guid? employeeId,
        Guid? employmentRecordId,
        Guid? statusHistoryId,
        string correlationId,
        string? idempotencyKeyHash,
        IReadOnlyDictionary<string, string> metadata)
    {
        if (!IsSafeMetadata(metadata))
        {
            throw new ArgumentException("Audit payload metadata contains prohibited PII or secret keys.", nameof(metadata));
        }

        return new EmployeeAuditPayload(
            eventName,
            tenantId,
            actorId,
            employeeId,
            employmentRecordId,
            statusHistoryId,
            correlationId,
            idempotencyKeyHash,
            metadata);
    }

    private static string JoinSafeFieldNames(IEnumerable<string> fieldNames)
        => string.Join(",", fieldNames
            .Select(field => field.Trim())
            .Where(field => field.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(field => field, StringComparer.OrdinalIgnoreCase));
}
