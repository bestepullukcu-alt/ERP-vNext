using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record DeleteQmsBaselineDefinitionCommand(
    Guid BaselineReleaseId,
    string CanonicalId,
    int VersionToken,
    string CorrelationId) : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Delete, "QmsBaselineDefinition",
        EntityId: BaselineReleaseId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?> { ["canonicalId"] = CanonicalId });
}
