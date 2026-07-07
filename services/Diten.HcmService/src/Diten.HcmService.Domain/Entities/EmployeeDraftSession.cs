namespace Diten.HcmService.Domain.Entities;

public sealed class EmployeeDraftSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public string DraftSchemaVersion { get; set; } = "employee-create-wizard.v1";
    public string CurrentStep { get; set; } = "draft-created";
    public Dictionary<string, EmployeeDraftStep> Steps { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> StepStatuses { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public EmployeeReferenceValidationSummary ReferenceValidationSummary { get; set; } = new();
    public string ReviewState { get; set; } = "not_reviewed";
    public List<string> ReviewBlockingReasons { get; set; } = [];
    public int Version { get; set; } = 1;
    public string ETag { get; set; } = CreateETag(1);
    public string? SourceContext { get; set; }
    public string? ClientReference { get; set; }
    public string CreateIdempotencyKeyHash { get; set; } = string.Empty;
    public List<string> OperationIdempotencyKeyHashes { get; set; } = [];
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddDays(30);
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static string CreateETag(int version) => $"\"{version}\"";

    public void Touch(string idempotencyKeyHash)
    {
        Version += 1;
        ETag = CreateETag(Version);
        UpdatedAt = DateTimeOffset.UtcNow;

        if (!OperationIdempotencyKeyHashes.Contains(idempotencyKeyHash, StringComparer.Ordinal))
        {
            OperationIdempotencyKeyHashes.Add(idempotencyKeyHash);
        }
    }
}

public sealed class EmployeeDraftStep
{
    public string StepCode { get; set; } = string.Empty;
    public string PayloadSchemaVersion { get; set; } = "employee-create-wizard.v1";
    public Dictionary<string, object?> Payload { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, object?> ClientValidationState { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class EmployeeReferenceValidationSummary
{
    public bool CanReview { get; set; }
    public DateTimeOffset? ValidatedAt { get; set; }
    public List<EmployeeReferenceValidationItem> Results { get; set; } = [];
}

public sealed class EmployeeReferenceValidationItem
{
    public string ReferenceType { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public string Status { get; set; } = "missing";
    public bool IsReferenceable { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string? ReasonCode { get; set; }
    public Dictionary<string, string> SafeDisplayMetadata { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
