using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record DeleteTenantAdminUserCommand(
    Guid TenantId,
    Guid AdminUserId) : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.IdentityAccess, Operation: AuditOperation.Delete, EntityType: "TenantAdminUser",
        EntityId: AdminUserId, TargetTenantId: TenantId, SourceModule: "tenant-registry");
}
