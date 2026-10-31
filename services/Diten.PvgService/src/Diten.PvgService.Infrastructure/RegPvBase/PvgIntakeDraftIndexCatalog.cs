namespace Diten.PvgService.Infrastructure.RegPvBase;

public static class PvgIntakeDraftIndexCatalog
{
    public const string CollectionName = "pvg_case_intake_drafts";

    public static IReadOnlyList<PvgPersistenceIndexDefinition> RequiredIndexes { get; } =
    [
        new(
            "ux_pvg_case_intake_drafts_tenant_intake",
            ["TenantId", "IntakeDraftId"],
            IsUnique: true,
            "Tenant-scoped lookup without cross-tenant existence leak",
            DescendingFields: []),
        new(
            "ix_pvg_case_intake_drafts_tenant_status_received",
            ["TenantId", "Status", "ReceivedAtUtc"],
            IsUnique: false,
            "Tenant-scoped list and status filtering",
            DescendingFields: []),
        // DEPO-03 S-6: the unfiltered list orders by received time descending, then draft id.
        new(
            "ix_pvg_case_intake_drafts_tenant_received",
            ["TenantId", "ReceivedAtUtc", "IntakeDraftId"],
            IsUnique: false,
            "Tenant-scoped list ordered by received time, newest first",
            DescendingFields: ["ReceivedAtUtc"])
    ];
}

// DescendingFields: the members of Fields that are indexed in descending order; every entry is one of Fields.
public sealed record PvgPersistenceIndexDefinition(
    string Name,
    IReadOnlyList<string> Fields,
    bool IsUnique,
    string Purpose,
    IReadOnlyList<string> DescendingFields);
