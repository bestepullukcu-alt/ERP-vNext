namespace Diten.HcmService.Domain.Entities;

public sealed class EmployeeDataQualityCase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid? EmployeeId { get; set; }
    public string CaseType { get; set; } = string.Empty;
    public string Severity { get; set; } = "low";
    public string Status { get; set; } = "open";
    public Guid? AssignedTo { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ResolvedAt { get; set; }
    public int Version { get; set; } = 1;
    public string ETag { get; set; } = CreateETag(1);
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static string CreateETag(int version) => $"\"{version}\"";
}
