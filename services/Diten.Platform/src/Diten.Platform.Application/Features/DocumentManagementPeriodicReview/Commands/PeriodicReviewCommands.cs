using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementPeriodicReview.Commands;

// MOD-0029-FU12 — periodic review / extension / overdue commands. Auditable via the central AuditBehavior. No hard delete.

internal static class PeriodicReviewAudit
{
    public const string Module = "MOD-0029-FU12";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

public sealed record InitiatePeriodicReviewCommand(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<PeriodicReviewModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => PeriodicReviewAudit.Meta(AuditOperation.Create, "DocumentPeriodicReview", RegisterEntryId, CorrelationId);
}

public sealed record CompletePeriodicReviewCommand(Guid RegisterEntryId, Guid ReviewId, CompletePeriodicReviewInput Input, string CorrelationId)
    : IRequest<Response<PeriodicReviewModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => PeriodicReviewAudit.Meta(AuditOperation.Update, "DocumentPeriodicReview", ReviewId, CorrelationId);
}

public sealed record RequestPeriodicReviewExtensionCommand(Guid RegisterEntryId, Guid ReviewId, RequestPeriodicReviewExtensionInput Input, string CorrelationId)
    : IRequest<Response<PeriodicReviewExtensionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => PeriodicReviewAudit.Meta(AuditOperation.Create, "DocumentPeriodicReviewExtension", ReviewId, CorrelationId);
}

public sealed record ApprovePeriodicReviewExtensionCommand(Guid RegisterEntryId, Guid ReviewId, Guid ExtensionId, ApprovePeriodicReviewExtensionInput Input, string CorrelationId)
    : IRequest<Response<PeriodicReviewExtensionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => PeriodicReviewAudit.Meta(AuditOperation.Update, "DocumentPeriodicReviewExtension", ExtensionId, CorrelationId);
}

public sealed record RejectPeriodicReviewExtensionCommand(Guid RegisterEntryId, Guid ReviewId, Guid ExtensionId, RejectPeriodicReviewExtensionInput Input, string CorrelationId)
    : IRequest<Response<PeriodicReviewExtensionModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => PeriodicReviewAudit.Meta(AuditOperation.Update, "DocumentPeriodicReviewExtension", ExtensionId, CorrelationId);
}

public sealed record EvaluatePeriodicReviewOverdueCommand(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<PeriodicReviewScheduleModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => PeriodicReviewAudit.Meta(AuditOperation.Execute, "DocumentPeriodicReview", RegisterEntryId, CorrelationId);
}
