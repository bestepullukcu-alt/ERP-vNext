using Diten.MdmService.Application.Contracts.Audit;
using Diten.Shared.Core;
using MediatR;

namespace Diten.MdmService.Application.Features.Brand.Commands;

// `Actor` is the audit identity (CreatedBy/UpdatedBy/ArchivedBy). MdmService has no ICurrentUser abstraction
// and introducing one would reach outside this pack's repo scope, so the controller reads the JWT subject and
// passes it in. It is audit metadata only — it is never used for authorization, which stays on [HasPermission].
//
// WP-MDM-BRAND-ACCEPT-01 FIX1 (item 1) — audited twice over: the central trail (IAuditableCommand → Platform, best effort)
// and the brand's own append-only history (BrandHistoryEntry), written in the same transaction as the brand, so a failed
// hand-over to Platform loses nothing. `CorrelationId` ties both to the request.
public sealed record CreateBrandCommand(BrandWriteRequest Request, string? Actor = null, string? CorrelationId = null)
    : IRequest<Response<Guid>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditMetadata GetAuditMetadata() => new(
        Category: AuditCategory.MasterData, Operation: AuditOperation.Create,
        EntityType: BrandAudit.EntityType, SourceModule: BrandAudit.SourceModule);
}

/// <summary>The audit identity every brand command reports under.</summary>
public static class BrandAudit
{
    public const string EntityType = "Brand";
    public const string SourceModule = "brand-product-master";
}
