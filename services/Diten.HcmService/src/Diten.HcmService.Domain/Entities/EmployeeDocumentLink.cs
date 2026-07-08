namespace Diten.HcmService.Domain.Entities;

public sealed class EmployeeDocumentLink
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid EvidenceId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string VisibilityLevel { get; set; } = "restricted_hr";
    public Guid RetentionPolicyId { get; set; }
    public Guid LinkedBy { get; set; }
    public DateTimeOffset LinkedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; } = 1;
    public string ETag { get; set; } = CreateETag(1);
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static string CreateETag(int version) => $"\"{version}\"";
}
