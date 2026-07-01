using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;

public sealed record BulkDeleteDocumentAccessPolicyCommand(IReadOnlyList<Guid> Ids, string CorrelationId)
    : IRequest<Response<int>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Delete,
        "DocumentAccessPolicy",
        SourceModule: "MOD-0029-FU04",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?> { ["count"] = Ids?.Count ?? 0 });
}
