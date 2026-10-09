using Diten.MdmService.Application.Contracts.Audit;
using Diten.MdmService.Domain.ValueObjects;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.StockBehaviour;

/// <summary>
/// WP-MDM-ITEM-MASTER (MOD-0290-FU04) S3 — who may edit stock behaviour. A constant of its own per resource (O-12); its
/// VALUE is for now the key that already edits the record's identity (CT decision 2026-10-07 (b), like the Finished Good
/// draft cancel), so Auth is not changed in this slice.
/// </summary>
public static class StockBehaviourPermissions
{
    // O-12: own key after the Auth role-matrix round (BL-573)
    public const string EditGsku = "mdm.gskus.update";

    // O-12: own key after the Auth role-matrix round (BL-573)
    public const string EditLsku = "mdm.lskus.create";

    // O-12: own key after the Auth role-matrix round (BL-573)
    public const string EditFinishedGood = "mdm.finished-goods.create";
}

/// <summary>
/// The GSKU's stock behaviour (pack §4.2). A null field is "not sent" — left as it is. The box ↔ pack conversion is
/// derived from the pack (O-14) and is not sent.
/// </summary>
public sealed record SetGskuStockBehaviourCommand(
    Guid GskuId,
    int ExpectedVersion,
    Guid OperationId,
    string? BaseUomCode = null,
    IReadOnlyList<ItemUomConversion>? UomConversions = null,
    int? ShelfLifeDays = null,
    IReadOnlyList<string>? StorageConditionCodes = null,
    bool? LotControlled = null,
    bool? Stockable = null)
    : IRequest<Response<StockBehaviourResult>>, IAuditableCommand, IAuditMetadataProvider,
      // CT decision R9: born fenced — no product write while the scope rollout measures the inventory.
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "Gsku", SourceModule: "product-item-sku-master");
}

/// <summary>The LSKU's own stock behaviour: serial control only (İTS follows the market); the rest is its GSKU's (O-3).</summary>
public sealed record SetLskuStockBehaviourCommand(
    Guid LskuId,
    int ExpectedVersion,
    Guid OperationId,
    bool? SerialControlled = null)
    : IRequest<Response<StockBehaviourResult>>, IAuditableCommand, IAuditMetadataProvider,
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "Lsku", SourceModule: "product-item-sku-master");
}

/// <summary>
/// The Finished Good's stock behaviour. Its base unit is its GSKU's (O-14) and is not sent; a shelf life left empty is
/// read from the GSKU (D-2); an own one never exceeds the GSKU's (O-15). The GTIN has its own command.
/// </summary>
public sealed record SetFinishedGoodStockBehaviourCommand(
    Guid FinishedGoodId,
    int ExpectedVersion,
    Guid OperationId,
    IReadOnlyList<ItemUomConversion>? UomConversions = null,
    int? ShelfLifeDays = null,
    IReadOnlyList<string>? StorageConditionCodes = null,
    bool? LotControlled = null,
    bool? SerialControlled = null,
    bool? Stockable = null)
    : IRequest<Response<StockBehaviourResult>>, IAuditableCommand, IAuditMetadataProvider,
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "FinishedGood", SourceModule: "product-item-sku-master");
}

/// <summary>The sales-box GTIN-14 of a Finished Good (the (01) of the İTS data matrix).</summary>
public sealed record AssignFinishedGoodGtinCommand(
    Guid FinishedGoodId,
    int ExpectedVersion,
    Guid OperationId,
    string Gtin)
    : IRequest<Response<StockBehaviourResult>>, IAuditableCommand, IAuditMetadataProvider,
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Assign,
        EntityType: "FinishedGood", SourceModule: "product-item-sku-master");
}

/// <param name="Changed">False when every sent value was already stored (nothing written, the version stays).</param>
public sealed record StockBehaviourResult(Guid RecordId, int Version, bool Changed, bool IsReplay);

public static class StockBehaviourErrors
{
    public const string Forbidden = "STOCK_BEHAVIOUR_FORBIDDEN";
    public const string Invalid = "STOCK_BEHAVIOUR_INVALID";
    public const string ReferenceListUnavailable = "STOCK_REFERENCE_LIST_UNAVAILABLE";
    public const string GskuBaseUomMissing = "FG_GSKU_BASE_UOM_MISSING";
    public const string GskuShelfLifeMissing = "FG_GSKU_SHELF_LIFE_MISSING";
}
