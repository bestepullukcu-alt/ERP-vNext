using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentRepository.Commands;

/// <summary>
/// MOD-0262-FU01 DL-2 — a trusted consumer service (v1: MOD-0217 through <c>Diten.MDM</c>) stores one binary under its
/// service identity. Audited on the central trail (path <c>a</c>); the actor is the service (<c>ActorType = Service</c>,
/// K-B). The metadata key set is the pack's §21 upload set, fixed here because <c>AuditBehavior</c> takes it before the
/// handler runs. The store-assigned <c>ContentId</c> does not exist yet at that moment, so the record carries no entity id.
/// </summary>
public sealed record StoreTrustedRepositoryObjectCommand(
    StoreTrustedRepositoryObjectInput Input,
    TrustedRepositoryConsumer Consumer,
    Guid CorrelationId)
    : IRequest<Response<RepositoryObjectModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Create,
        "RepositoryObject",
        SourceModule: "MOD-0262-FU01",
        CorrelationId: CorrelationId,
        Metadata: TrustedRepositoryAuditMetadata.For(Consumer, Input.Scope.ToString(), "store"));
}
