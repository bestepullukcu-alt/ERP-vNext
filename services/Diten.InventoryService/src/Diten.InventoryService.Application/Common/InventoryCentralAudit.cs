using Diten.BuildingBlocks.CentralAudit;
using Diten.InventoryService.Application.Common.Models;

namespace Diten.InventoryService.Application.Common;

/// <summary>
/// BL-501 — what every Inventory write command tells Platform's central audit log (AUD-001 path b, shared forwarder).
/// The in-product trails (stock ledger audit, quarantine history, recall timeline, expiry events, lot / location
/// history) are unchanged: the central record is written NEXT to them, in the same Mongo transaction
/// (<c>CentralAuditTransaction.EnlistAsync</c> in each repository's transaction callback).
///
/// <para><b>Category.</b> Platform's <c>AuditCategory</c> has no inventory / GxP-operations value (measured 2026-10-14:
/// 17 names, the closest is <c>MasterData</c>, which MVP-3 also uses for BOMs). Every Inventory record is filed under
/// <see cref="Category"/>; the module code and entity type say what it is. A dedicated category is a Platform enum + Web
/// label change, left to Control Tower (evidence: BL-501 "açık konular").</para>
///
/// <para><b>Source module</b> = the module code the service's manifest registers with Platform
/// (<c>InventoryCentralAuditDeclarationTests</c> compares them with the manifest providers).</para>
/// </summary>
public static class InventoryCentralAudit
{
    public const string Category = "MasterData";

    public const string LedgerModule = "INVENTORY-LEDGER";
    public const string LotSerialModule = "LOT-SERIAL-TRACKING";
    public const string QuarantineModule = "QUARANTINE-BLOCKED-STOCK";
    public const string ExpiryModule = "EXPIRY-FEFO-MANAGEMENT";
    public const string RecallModule = "RECALL-READINESS";

    public static CentralAuditMetadata ForLedger(string operation, string entityType) => new(Category, operation, entityType, LedgerModule);

    public static CentralAuditMetadata ForLotSerial(string operation, string entityType) => new(Category, operation, entityType, LotSerialModule);

    public static CentralAuditMetadata ForQuarantine(string operation, string entityType) => new(Category, operation, entityType, QuarantineModule);

    public static CentralAuditMetadata ForExpiry(string operation, string entityType) => new(Category, operation, entityType, ExpiryModule);

    public static CentralAuditMetadata ForRecall(string operation, string entityType) => new(Category, operation, entityType, RecallModule);

    /// <summary>The success payload of the handler's envelope, or null (failed envelope / other type).</summary>
    public static T? Data<T>(object? response) where T : class => (response as Response<T>)?.Data;

    /// <summary>An id as Platform stores it (a Guid) — from a Guid, or a string that is one; anything else is "no id".</summary>
    public static Guid? Id(object? value) => value switch
    {
        Guid guid when guid != Guid.Empty => guid,
        string text when Guid.TryParse(text, out var parsed) && parsed != Guid.Empty => parsed,
        _ => null
    };
}
