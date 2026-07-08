namespace Diten.HcmService.Domain.Entities;

public sealed class Employee
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public string EmployeeNumber { get; set; } = string.Empty;
    public string LegalFirstName { get; set; } = string.Empty;
    public string? LegalMiddleName { get; set; }
    public string LegalLastName { get; set; } = string.Empty;
    public string? PreferredName { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? NationalityCode { get; set; }
    public string? WorkEmail { get; set; }
    public string? PersonalEmail { get; set; }
    public string? Phone { get; set; }
    public string EmployeeStatus { get; set; } = "draft";
    public string WorkerType { get; set; } = string.Empty;
    public string EmploymentType { get; set; } = string.Empty;
    public DateOnly? HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public string SensitivityLevel { get; set; } = "standard";
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public int Version { get; set; } = 1;
    public string ETag { get; set; } = CreateETag(1);
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }

    public static string CreateETag(int version) => $"\"{version}\"";
}
