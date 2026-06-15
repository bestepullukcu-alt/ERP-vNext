using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record PublishBusinessReferenceDataVersionCommand(
    Guid VersionId,
    string ActorId,
    string CorrelationId,
    string IdempotencyKey,
    string PublishMode,
    DateTimeOffset? PublishAt,
    string? ExpectedConcurrencyToken,
    bool OverrideAction,
    string? OverrideReason)
    : IRequest<Response<BusinessReferenceDataVersionDetailModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Activate,
        "BusinessReferenceDataVersion",
        EntityId: VersionId,
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?>
        {
            ["governanceEvent"] = "publish",
            ["publishMode"] = PublishMode,
            ["override"] = OverrideAction,
            ["overrideReason"] = OverrideReason,
            ["actorId"] = ActorId,
            ["correlationId"] = CorrelationId,
            ["affectedVersionId"] = VersionId
        });
}
