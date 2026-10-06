using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.ProductItemSkuMaster.Lifecycle.Commands;

// LSKU FIX1 B1 (c): operator repair for admissions a failed LSKU creation left on its parent GSKU before FIX1.
// Tenant-bounded (the repositories read the request's tenant); the endpoint is held by the non-human recover key.
public sealed record RepairLskuChildAdmissionsCommand(int Limit = 100, int MinimumAgeMinutes = 15)
    : IRequest<Response<LskuChildAdmissionRepairResult>>, IAuditableCommand, IAuditMetadataProvider
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
