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
            "Tenant-scoped lookup without cross-tenant existence leak"),
        new(
            "ix_pvg_case_intake_drafts_tenant_status_received",
            ["TenantId", "Status", "ReceivedAtUtc"],
            IsUnique: false,
            "Tenant-scoped list and status filtering")
    ];
}

public sealed record PvgPersistenceIndexDefinition(
    string Name,
    IReadOnlyList<string> Fields,
    bool IsUnique,
    string Purpose);
