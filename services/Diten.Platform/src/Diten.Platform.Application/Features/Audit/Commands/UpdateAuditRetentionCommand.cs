using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.Audit.Commands;

public sealed record UpdateAuditRetentionCommand(UpdateAuditRetentionRequest Request)
    : IRequest<Response<AuditRetentionPolicyDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        Category: AuditCategory.System, Operation: AuditOperation.Update, EntityType: "AuditRetentionPolicy",
        SourceModule: "audit", IsPlatformGlobal: true);
}
