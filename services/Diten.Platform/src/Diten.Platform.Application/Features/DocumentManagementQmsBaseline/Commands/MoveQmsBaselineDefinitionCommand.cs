using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record MoveQmsBaselineDefinitionCommand(
    Guid BaselineReleaseId,
    string CanonicalId,
    QmsCollectionDefinitionMoveModel Request,
    string CorrelationId) : IRequest<Response<QmsCollectionDefinitionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "QmsBaselineDefinition",
        EntityId: BaselineReleaseId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?> { ["canonicalId"] = CanonicalId });
}
