using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementAccessProfileTemplates.Commands;

/// <summary>MOD-0029-FU05 — preview the access-profile policy generation for a baseline. Writes nothing.</summary>
public sealed record DryRunAccessProfileTemplatesCommand(
    AccessProfileTemplateRequest Request,
    string CorrelationId) : IRequest<Response<AccessProfileTemplateSummary>>;

/// <summary>MOD-0029-FU05 — idempotently apply generated access-profile policies (Effective/Published only).</summary>
public sealed record ApplyAccessProfileTemplatesCommand(
    AccessProfileTemplateRequest Request,
    string CorrelationId) : IRequest<Response<AccessProfileTemplateSummary>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Create, "AccessProfilePolicyTemplate",
        EntityId: Request.BaselineReleaseId, SourceModule: "MOD-0029",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
