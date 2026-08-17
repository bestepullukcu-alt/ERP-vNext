using Diten.Platform.Application.Features.ModuleCatalog;
using Diten.Platform.Application.Features.ModuleCatalog.Queries;
using Diten.Platform.Common.Catalog;
using Diten.Platform.Domain.Entities;

namespace Diten.Platform.Application.Features.ModuleAssignments.Handlers.QueryHandlers;

internal static class ModuleAssignmentQueryHelpers
{
    public const string TenantDependencySource = "TenantModuleAssignment";
    public const string UnavailableStatus = "Unavailable";
    public const string AvailableStatus = "Available";
    public const string PlanEntitlementStatus = "Included";

    public static async Task<AssignableModuleInfo?> GetModuleAsync(
        IPlatformCatalogContract catalogContract,
        string moduleCode,
        CancellationToken ct)
    {
        var canonicalCode = ModuleCatalogCodeNormalizer.Normalize(moduleCode);
        if (string.IsNullOrWhiteSpace(canonicalCode))
        {
            return null;
        }

        var modules = await catalogContract.GetAssignableModulesAsync(ct);
        return modules.FirstOrDefault(x => string.Equals(x.ModuleCode, canonicalCode, StringComparison.OrdinalIgnoreCase));
    }

    public static ModuleAssignmentDependencyStateDto TenantDependencyUnavailable() =>
        new(
            TenantDependencySource,
            UnavailableStatus,
            "Tenant Module Assignment read source is not available yet. Tenant assignment rows are intentionally degraded instead of synthesized.");

    public static ModulePlanAssignmentRowDto ToPlanRow(SubscriptionPlan plan) =>
        new(
            plan.Code,
            plan.Name,
            plan.IsActive ? "Active" : "Inactive",
            PlanEntitlementStatus,
            true,
            null,
            null,
            plan.UpdatedAt ?? plan.CreatedAt);

    public static IReadOnlyList<ModulePlanAssignmentRowDto> ApplyPlanFilters(
        IReadOnlyList<ModulePlanAssignmentRowDto> rows,
        ModulePlanAssignmentFilterRequest filter)
    {
        IEnumerable<ModulePlanAssignmentRowDto> query = rows;

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            query = query.Where(x => string.Equals(x.PlanStatus, filter.Status.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(x.EntitlementStatus, filter.Status.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim();
            query = query.Where(x =>
                x.PlanCode.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.PlanName.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.PlanStatus.Contains(search, StringComparison.OrdinalIgnoreCase)
                || x.EntitlementStatus.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query.ToList();
    }

    public static bool IsValidTenantSource(string? source) =>
        string.IsNullOrWhiteSpace(source)
        || source is "Plan" or "Manual" or "Trial" or "Override" or "System";

    public static bool IsValidTenantStatus(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || status is "Enabled" or "Disabled" or "Suspended" or "Pending" or "Expired";
}
