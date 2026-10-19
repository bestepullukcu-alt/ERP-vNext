using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Commands;

// Soft archive only. There is deliberately no DeleteBrandCommand / BulkDeleteBrandCommand: FU01 §3 forbids
// hard delete, so the capability does not exist at any layer (command, handler, controller, gateway route).
//
// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — see CreateBrandCommand for the two trails. Platform's audit enum has no Archive;
// a soft archive reports as Deactivate (the same mapping ArchiveLegalEntityCommand uses).
public sealed record ArchiveBrandCommand(Guid BrandId, string? Actor = null, string? CorrelationId = null)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Deactivate,
        EntityType: BrandAudit.EntityType, SourceModule: BrandAudit.SourceModule);
}
