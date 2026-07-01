using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTemplateVariants.Commands;

public sealed record RebaseTemplateVariantCommand(Guid Id, RebaseTemplateVariantInput Input, string CorrelationId)
    : IRequest<Response<TemplateVariantDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement,
        AuditOperation.Update,
        "TemplateVariant",
        EntityId: Id,
        SourceModule: "MOD-0029-FU03",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null);
}
