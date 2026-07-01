using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessMatrix.Commands;

public sealed record CreateDocumentAccessPolicyCommand(DocumentAccessPolicyInput Input, string CorrelationId)
    : IRequest<Response<DocumentAccessPolicyDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Create,
        "DocumentAccessPolicy",
        SourceModule: "MOD-0029-FU04",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?>
        {
            ["targetType"] = Input?.TargetType,
            ["principalType"] = Input?.PrincipalType,
            ["effect"] = Input?.Effect
        });
}
