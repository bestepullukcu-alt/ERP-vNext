namespace Diten.ManufacturingService.Application.Common;

/// <summary>
/// AUD-001 K4 / BL-501 pre-wiring — what each BOM write command will tell Platform's central audit log.
///
/// The owner's decision (2026-10-12): MOD-0193 writes to the central log IN ADDITION to the BOM version history (path c,
/// <c>bom-surum-gecmisi</c>), which stays as the user-facing trail. The forwarder itself is BL-501's: one marker, one
/// pipeline behaviour and one Platform client in <c>Diten.Building.Blocks</c>. Copying MDM's
/// <c>AuditForwardingBehavior</c> here is forbidden (AUD-001 K4), so this file holds NO behaviour, NO HTTP client and
/// NO registration — only the data. When BL-501 lands, each command adds the shared marker and returns
/// <see cref="CentralAudit"/> from it, and <c>Program.cs</c> registers the shared behaviour: wiring, nothing to design.
///
/// Values are Platform's own enum NAMES (<c>Diten.Platform.Domain.Enums.AuditCategory</c> / <c>AuditOperation</c>) so the
/// record lands in a category Platform knows; <c>CentralAuditDeclarationTests</c> reads that file and fails if one is not.
/// </summary>
public interface ICentralAuditDeclaration
{
    CentralAuditMetadata CentralAudit { get; }

    /// <summary>
    /// The audited entity's id. Explicit, not inferred: a create command carries no id and returns it inside
    /// <c>BomView</c>, which a "Data is Guid / property ending in Id" lookup cannot see.
    /// </summary>
    Guid? CentralAuditEntityId(object? response);
}

public sealed record CentralAuditMetadata(string Category, string Operation, string EntityType, string SourceModule)
{
    public const string BomCategory = "MasterData";
    public const string BomEntityType = "BomVersion";

    /// <summary>The module code the manifest registers with Platform (<c>BomRoutingsManifestProvider</c>).</summary>
    public const string BomSourceModule = "bom-routings";

    public static CentralAuditMetadata Bom(string operation) => new(BomCategory, operation, BomEntityType, BomSourceModule);
}
