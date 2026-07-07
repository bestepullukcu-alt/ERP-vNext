namespace Diten.HcmService.Domain.Entities;

public sealed class EmploymentRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid LegalEntityId { get; set; }
    public Guid OrganizationUnitId { get; set; }
    public Guid PositionId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string ContractType { get; set; } = string.Empty;
    public string? ProbationStatus { get; set; }
    public DateOnly? ProbationEndDate { get; set; }
    public string EmploymentStatus { get; set; } = "draft";
    public string? TerminationReasonCategory { get; set; }
    public string? RehireEligibility { get; set; }
    public string SourceCreationMethod { get; set; } = "manual_entry";
    public string ApprovalStatus { get; set; } = "draft";
    public int Version { get; set; } = 1;
    public string ETag { get; set; } = CreateETag(1);
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static string CreateETag(int version) => $"\"{version}\"";
}
