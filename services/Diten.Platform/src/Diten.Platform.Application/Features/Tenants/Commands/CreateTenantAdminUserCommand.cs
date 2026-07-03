using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Tenants.Commands;

public sealed record CreateTenantAdminUserCommand(
    Guid TenantId,
    TenantAdminUserUpsertRequest Request) : IRequest<Response<TenantAdminUserDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.IdentityAccess, Operation: AuditOperation.Create, EntityType: "TenantAdminUser",
        TargetTenantId: TenantId, SourceModule: "tenant-registry");
}
