using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record UpdateTenantAdminUserCommand(
    Guid TenantId,
    Guid AdminUserId,
    TenantAdminUserUpsertRequest Request) : IRequest<Response<TenantAdminUserDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.IdentityAccess, Operation: AuditOperation.Update, EntityType: "TenantAdminUser",
        EntityId: AdminUserId, TargetTenantId: TenantId, SourceModule: "tenant-registry");
}
