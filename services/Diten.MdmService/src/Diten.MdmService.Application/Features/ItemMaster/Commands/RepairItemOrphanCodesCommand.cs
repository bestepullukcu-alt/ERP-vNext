using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ItemMaster.Commands;

// MOD-0290-FU04 S2 (S1 carry-over c): the operator repair — IT- codes consumed for an item that was never written, older
// than the minimum age, are burned (never handed out again; the burn checks "no item row" in its own transaction).
// Path b; each burn also writes the reservation's own trusted-source intent (CodeBurned).
public sealed record RepairItemOrphanCodesCommand(int Limit, int MinimumAgeMinutes)
    : IRequest<Response<ItemOrphanCodeRepairResult>>, IAuditableCommand, IAuditMetadataProvider, IAuditDetailsProvider
{
    public AuditMetadata GetAuditMetadata() => ItemAudit.Metadata(AuditOperation.Execute);
    public AuditDetails AuditDetails { get; } = new();
}

public sealed record ItemOrphanCodeRepairResult(int Found, int Burned, int NotConsumed, int Refused,
    IReadOnlyList<string> RefusalCodes);

public static class ItemOrphanCodes
{
    // A creation takes seconds; a code held longer than this with no row is a leak.
    public static readonly TimeSpan MinimumAge = TimeSpan.FromMinutes(15);
    // S2 FIX1 (minor): the repair never burns what the gauge does not yet count as a leak — its floor IS the gauge's age
    // (15, not the LSKU / FG repair's 5): an item create whose answer was lost is repeated by its client with the same
    // creation key, and that repeat consumes the held code; burnt earlier, the repeat fails with a code that was never
    // a leak. Above: one year (a larger value only narrows the sweep; past it the cutoff leaves the clock's range → 500).
    public const int MinimumAgeFloorMinutes = 15;
    public const int MinimumAgeCeilingMinutes = 525_600;
    public const string BurnReason = "ITEM_ORPHAN_CODE_REPAIRED";
}
