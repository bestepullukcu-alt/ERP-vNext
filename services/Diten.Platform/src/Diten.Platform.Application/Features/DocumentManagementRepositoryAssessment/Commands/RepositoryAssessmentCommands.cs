using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementRepositoryAssessment.Commands;

// MOD-0029-FU16 — repository assessment commands. Auditable via the central AuditBehavior. No hard delete.

internal static class RepositoryAssessmentAudit
{
    public const string Module = "MOD-0029-FU16";
    public static Guid? Correlation(string? c) => Guid.TryParse(c, out var g) ? g : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

public sealed record CreateRepositoryAssessmentCommand(RepositoryAssessmentFieldsInput Input, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RepositoryAssessmentAudit.Meta(AuditOperation.Create, "DocumentRepositoryAssessment", Guid.Empty, CorrelationId);
}

public sealed record UpdateRepositoryAssessmentCommand(Guid Id, RepositoryAssessmentFieldsInput Input, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RepositoryAssessmentAudit.Meta(AuditOperation.Update, "DocumentRepositoryAssessment", Id, CorrelationId);
}

public sealed record EvaluateRepositoryAssessmentCommand(Guid Id, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentReadinessModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RepositoryAssessmentAudit.Meta(AuditOperation.Execute, "DocumentRepositoryAssessment", Id, CorrelationId);
}

public sealed record ApproveRepositoryAssessmentCommand(Guid Id, ApproveRepositoryAssessmentInput Input, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RepositoryAssessmentAudit.Meta(AuditOperation.Update, "DocumentRepositoryAssessment", Id, CorrelationId);
}

public sealed record RejectRepositoryAssessmentCommand(Guid Id, RejectRepositoryAssessmentInput Input, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RepositoryAssessmentAudit.Meta(AuditOperation.Update, "DocumentRepositoryAssessment", Id, CorrelationId);
}

public sealed record LinkRepositoryAssessmentToRegisterEntryCommand(Guid RegisterEntryId, LinkRepositoryAssessmentInput Input, string CorrelationId)
    : IRequest<Response<RepositoryAssessmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => RepositoryAssessmentAudit.Meta(AuditOperation.Assign, "DocumentMasterRegisterEntry", RegisterEntryId, CorrelationId);
}
