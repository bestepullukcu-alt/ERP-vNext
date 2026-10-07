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
    // WP-MDM-SCOPE-INT-01 FIX1 M2: one id per call, so the writer lease is per run. Before, it was the payload's hash: two
    // overlapping runs with the same parameters shared one lease and the second ran on without one once the first
    // released it. Text, not a Guid, on purpose: the path-b audit names the first Guid "…Id" of a command as its
    // entity, and a run id is not a GSKU.
    public string CommandId { get; init; } = Guid.NewGuid().ToString("D");

    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: "Gsku", SourceModule: "product-item-sku-master");
}

/// <param name="Found">The orphans found inside the caller's background scope (all of them outside an Enforced tenant).</param>
/// <param name="OutOfScope">WP-MDM-SCOPE-INT-01 FIX1 item 1: orphans of out-of-scope or unclassified products in an
/// Enforced tenant — skipped, counted, never named.</param>
public sealed record LskuChildAdmissionRepairResult(
    int Found,
    int Released,
    IReadOnlyList<LskuChildAdmissionRepairItem> Items,
    int OutOfScope = 0);

public sealed record LskuChildAdmissionRepairItem(
    Guid GskuId,
    string CreationCommandId,
    DateTimeOffset AcquiredAtUtc,
    bool Released,
    string? RefusalCode);
