using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

// LSKU FIX1 B1 (c): operator repair for admissions a failed LSKU creation left on its parent GSKU before FIX1.
// Tenant-bounded (the repositories read the request's tenant); the endpoint is held by the non-human recover key.
// FG FIX1 item 2: the same operator repair for a Finished Good's admissions (the release path is child-kind independent).
public sealed record RepairLskuChildAdmissionsCommand(int Limit = 100, int MinimumAgeMinutes = 15,
    Diten.MdmService.Domain.ValueObjects.GskuChildIdentityKind ChildKind = Diten.MdmService.Domain.ValueObjects.GskuChildIdentityKind.Lsku)
    : IRequest<Response<LskuChildAdmissionRepairResult>>, IAuditableCommand, IAuditMetadataProvider,
      // WP-MDM-SCOPE-INT-01 (CT decision): born fenced — the repair changes a GSKU's admissions; while a fence measures
      // the inventory the operator retries after it closes.
      global::Diten.MdmService.Application.Features.ProductLegalEntityScopes.IProductLegalEntityScopeInventoryMutation
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "Gsku", SourceModule: "product-item-sku-master");
}

public sealed record LskuChildAdmissionRepairResult(
    int Found,
    int Released,
    IReadOnlyList<LskuChildAdmissionRepairItem> Items);

public sealed record LskuChildAdmissionRepairItem(
    Guid GskuId,
    string CreationCommandId,
    DateTimeOffset AcquiredAtUtc,
    bool Released,
    string? RefusalCode);
