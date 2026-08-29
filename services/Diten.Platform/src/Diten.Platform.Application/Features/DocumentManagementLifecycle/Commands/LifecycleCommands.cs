using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementLifecycle.Commands;

// MOD-0029-FU08 — controlled document lifecycle transition command. Auditable via the central AuditBehavior. No hard
// delete: a transition is a forward status change plus a permanent transition record.

public sealed record TransitionDocumentLifecycleCommand(Guid RegisterEntryId, TransitionDocumentLifecycleInput Input, string CorrelationId)
    : IRequest<Response<LifecycleStateModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.DocumentManagement, AuditOperation.Update, "DocumentMasterRegisterEntry",
        EntityId: RegisterEntryId, SourceModule: "MOD-0029-FU08",
        CorrelationId: Guid.TryParse(CorrelationId, out var c) ? c : null,
        Metadata: new Dictionary<string, object?> { ["targetStatus"] = Input.TargetStatus });
}
