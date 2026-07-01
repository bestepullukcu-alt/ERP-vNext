using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;

public sealed record UpdateDocumentAccessPolicyCommand(Guid Id, DocumentAccessPolicyInput Input, string CorrelationId)
    : IRequest<Response<DocumentAccessPolicyDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Update,
        "DocumentAccessPolicy",
        EntityId: Id,
        SourceModule: "MOD-0029-FU04",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
