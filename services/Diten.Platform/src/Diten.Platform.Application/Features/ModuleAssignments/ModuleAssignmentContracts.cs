using Diten.Platform.Application.Features.Tenants;

namespace Diten.Platform.Application.Features.ModuleAssignments;

public sealed record ModuleAssignmentDependencyStateDto(
    string Source,
    string Status,
    string? Message);

public sealed record ModuleAssignmentOverviewDto(
    string ModuleCode,
    string ModuleName,
    string ModuleStatus,
    int PlanAssignmentCount,
    int? TenantAssignmentCount,
    int? EnabledTenantCount,
    int? DisabledTenantCount,
    int? ManualOverrideCount,
    int? PlanDerivedCount,
    DateTimeOffset? LastAssignmentChangedAtUtc,
    IReadOnlyList<ModuleAssignmentDependencyStateDto> DependencyStates,
    string CorrelationId);

public sealed record ModulePlanAssignmentRowDto(
    string PlanCode,
    string PlanName,
    string PlanStatus,
    string EntitlementStatus,
    bool IncludedByDefault,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    DateTimeOffset? LastUpdatedAtUtc);

public sealed record ModuleTenantAssignmentRowDto(
    string TenantCode,
    string TenantName,
    string TenantStatus,
    string AssignmentStatus,
    string AssignmentSource,
    string? SourcePlanCode,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    DateTimeOffset? AssignedAtUtc,
    string? AssignedBy,
    DateTimeOffset? LastUpdatedAtUtc);

public sealed record ModuleTenantAssignmentDetailDto(
    string TenantCode,
    string TenantName,
    string TenantStatus,
    string AssignmentStatus,
    string AssignmentSource,
    string? SourcePlanCode,
    DateTimeOffset? EffectiveFromUtc,
    DateTimeOffset? EffectiveToUtc,
    DateTimeOffset? AssignedAtUtc,
    string? AssignedBy,
    DateTimeOffset? LastUpdatedAtUtc,
    string? AssignmentReason,
    string? EffectiveStatusReason,
    string? SourceEvidenceType,
    string? SourceEvidenceReference,
    DateTimeOffset? CreatedAtUtc,
    string? CreatedBy,
    DateTimeOffset? LastChangedAtUtc,
    string? LastChangedBy,
    bool AuditEvidenceAvailable,
    string CorrelationId);

public sealed record ModuleAssignmentPageDto<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long TotalCount,
    int TotalPages,
    IReadOnlyList<ModuleAssignmentDependencyStateDto> DependencyStates);

public sealed record ModulePlanAssignmentFilterRequest(
    string? Status,
    string? Search,
    int Page = 1,
    int PageSize = 20);

public sealed record ModuleTenantAssignmentFilterRequest(
    string? Source,
    string? Status,
    string? TenantStatus,
    string? Search,
    int Page = 1,
    int PageSize = 20);

internal static class ModuleAssignmentPaging
{
    public static ModuleAssignmentPageDto<T> ToPage<T>(
        IReadOnlyList<T> source,
        int page,
        int pageSize,
        IReadOnlyList<ModuleAssignmentDependencyStateDto>? dependencyStates = null)
    {
        var normalizedPage = Math.Max(page, 1);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);
        var total = source.Count;
        var items = source
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToList();
        var totalPages = total == 0 ? 0 : (int)Math.Ceiling(total / (double)normalizedPageSize);

        return new ModuleAssignmentPageDto<T>(
            items,
            normalizedPage,
            normalizedPageSize,
            total,
            totalPages,
            dependencyStates ?? []);
    }
}
