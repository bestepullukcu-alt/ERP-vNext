using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementQmsBaseline.Commands;

public sealed record PublishQmsBaselineCommand(
    Guid BaselineReleaseId,
    int ExpectedVersion,
    string CorrelationId) : IRequest<Response<QmsBaselinePublishResult>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Activate, "QmsBaseline",
        EntityId: BaselineReleaseId, SourceModule: "MOD-0028", CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
