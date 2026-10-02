using MongoDB.Bson.Serialization.Attributes;

namespace Diten.AuthService.Persistence.Seed;

/// <summary>
/// BL-452 package 3 / WP-ROLES-CLOSE-01 — "this tenant is settled for the one-way backfill of <see cref="Key"/>": the
/// persistent gate of <c>ExportGrantBackfillRunner</c>. One row per (tenant, key), with a deterministic id
/// (<c>ExportGrantBackfill.MarkId</c>) so two writers produce the same document. <see cref="Origin"/> says why: the
/// tenant was BORN with the key (never backfilled) or it was an old tenant and has been BACKFILLED (never looked at
/// again, so an administrator who takes the permission away keeps it away across restarts). Seed bookkeeping only —
/// no API reads or writes it.
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

    /// <summary>
    /// How the tenant came to be settled for this key (<c>ExportGrantBackfill.OriginBorn</c> /
    /// <c>OriginBackfilled</c>). Null on rows written before the origin was recorded — those were all backfill marks.
    /// </summary>
    public string? Origin { get; set; }
}
