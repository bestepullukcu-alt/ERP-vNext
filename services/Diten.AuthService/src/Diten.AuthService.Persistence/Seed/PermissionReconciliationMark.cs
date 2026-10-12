using MongoDB.Bson.Serialization.Attributes;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// BL-452 package 3 (v2) — "this tenant has been through the one-way backfill of <see cref="Key"/>": the persistent gate
/// of <c>DataSeeder.BackfillUsersExportAsync</c>. One row per (tenant, key); a marked tenant is never backfilled again,
/// so an administrator who takes the permission away keeps it away across restarts. Seed bookkeeping only — no API
/// reads or writes it.
/// </summary>
[BsonIgnoreExtraElements]
public sealed class PermissionReconciliationMark
{
    public const string CollectionName = "permissionReconciliations";

    [BsonId]
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TenantId { get; set; }

    public string Key { get; set; } = string.Empty;

    public DateTime ReconciledAtUtc { get; set; }
}
