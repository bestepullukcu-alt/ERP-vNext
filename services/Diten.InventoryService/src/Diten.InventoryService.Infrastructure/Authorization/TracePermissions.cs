namespace Diten.InventoryService.Infrastructure.Authorization;

/// <summary>
/// MOD-0174 permission keys — namespace <c>trace.*</c>, owned by MOD-0174 (pack §14, CT-D5; PKS-001 §4: one owner per
/// namespace, <c>inventory.*</c> stays MOD-0173's). Tier-3 actions registered by the pack (PKS-001 §5):
/// <c>update-released</c>, <c>change-status</c>, <c>migrate</c>, <c>link</c>.
/// </summary>
public static class TracePermissions
{
    public const string LotsRead = "trace.lots.read";

    public const string LotsCreate = "trace.lots.create";

    /// <summary>Edit a Draft lot.</summary>
    public const string LotsUpdate = "trace.lots.update";

    /// <summary>K-C: edit a lot that is not Draft (Released / Blocked / Expired — CT-D8), with a reason.</summary>
    public const string LotsUpdateReleased = "trace.lots.update-released";

    /// <summary>K-E: Draft → Released, Released → Blocked, Blocked → Released.</summary>
    public const string LotsChangeStatus = "trace.lots.change-status";

    /// <summary>Typed-lot migration (slice P4) — administrator, one-off.</summary>
    public const string LotsMigrate = "trace.lots.migrate";

    public const string SerialsRead = "trace.serials.read";

    /// <summary>API-only (MOD-0194 writes genealogy; no screen) — declared in the manifest's ApiPermissions (standard §2d).</summary>
    public const string GenealogyLink = "trace.genealogy.link";
}
