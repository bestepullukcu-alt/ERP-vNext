namespace Diten.HcmService.Application.Features.CoreHrEmployeeMaster;

public static class EmployeeReferenceDataContracts
{
    public static readonly IReadOnlySet<string> EmployeeStatuses = Set(
        "draft",
        "pending_approval",
        "active",
        "on_leave",
        "suspended",
        "terminated",
        "rehired",
        "rejected",
        "abandoned");

    public static readonly IReadOnlySet<string> EmploymentStatuses = Set(
        "draft",
        "active",
        "leave",
        "suspended",
        "terminated");

    public static readonly IReadOnlySet<string> WorkerTypes = Set(
        "employee",
        "contractor",
        "intern",
        "consultant",
        "other");

    public static readonly IReadOnlySet<string> EmploymentTypes = Set(
        "full_time",
        "part_time",
        "temporary",
        "contract");

    public static readonly IReadOnlySet<string> ContractTypes = Set(
        "permanent",
        "fixed_term",
        "contractor",
        "internship");

    public static readonly IReadOnlySet<string> TerminationReasonCategories = Set(
        "voluntary",
        "involuntary",
        "end_of_contract",
        "retirement",
        "redundancy",
        "other");

    public static readonly IReadOnlySet<string> DataQualityCaseTypes = Set(
        "duplicate_candidate",
        "missing_required_data",
        "invalid_status",
        "conflicting_identifier");

    public static readonly IReadOnlySet<string> DataQualityCaseStatuses = Set(
        "open",
        "in_review",
        "resolved",
        "rejected");

    public static readonly IReadOnlySet<string> SensitivityLevels = Set(
        "standard",
        "restricted",
        "legal_only");

    public static bool IsAllowed(IReadOnlySet<string> allowedValues, string? value)
        => !string.IsNullOrWhiteSpace(value) && allowedValues.Contains(value.Trim());

    public static IReadOnlyList<ReferenceDataSeedContract> SeedContracts =>
    [
        new("employee_status", EmployeeStatuses),
        new("employment_status", EmploymentStatuses),
        new("worker_type", WorkerTypes),
        new("employment_type", EmploymentTypes),
        new("contract_type", ContractTypes),
        new("termination_reason_category", TerminationReasonCategories),
        new("data_quality_case_type", DataQualityCaseTypes)
    ];

    private static IReadOnlySet<string> Set(params string[] values)
        => new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
}

public sealed record ReferenceDataSeedContract(string Category, IReadOnlySet<string> Codes);
