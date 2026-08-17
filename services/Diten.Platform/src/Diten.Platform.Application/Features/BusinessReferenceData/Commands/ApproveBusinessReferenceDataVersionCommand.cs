using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.BusinessReferenceData.Models;
using Diten.Platform.Application.Features.BusinessReferenceData.Services;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.BusinessReferenceData.Commands;

public sealed record ApproveBusinessReferenceDataVersionCommand(
    Guid VersionId,
    string ActorId,
    string CorrelationId,
    string? ExpectedConcurrencyToken,
    BusinessReferenceDataWorkflowTransitionAction Action,
    string? RejectionReason,
    bool OverrideAction,
    string? OverrideReason,
    BusinessReferenceDataEvidenceInput Evidence,
    string? RequestInfoComment,
    string? RequestInfoTargetStep,
    string? IdempotencyKey)
    : IRequest<Response<BusinessReferenceDataVersionDetailModel>>, IBusinessReferenceDataRequest, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.ReferenceData,
        AuditOperation.Execute,
        "BusinessReferenceDataVersion",
        EntityId: VersionId,
        SourceModule: "PSS-012",
        Metadata: new Dictionary<string, object?> { ["governanceEvent"] = "approve", ["action"] = Action.ToString(), ["override"] = OverrideAction });
}
