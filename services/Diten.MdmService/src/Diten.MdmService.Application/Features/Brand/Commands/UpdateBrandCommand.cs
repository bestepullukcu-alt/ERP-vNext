using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Commands;

// WP-MDM-BRAND-ACCEPT-01 FIX1 (items 1, 2) — see CreateBrandCommand for the two trails; the edit is conditional on
// Request.ExpectedVersion.
public sealed record UpdateBrandCommand(Guid BrandId, BrandWriteRequest Request, string? Actor = null, string? CorrelationId = null)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Update,
        EntityType: BrandAudit.EntityType, SourceModule: BrandAudit.SourceModule);
}
