using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.DocumentManagementTraining.Commands;

// MOD-0029-FU11 — training matrix + assignment commands. Auditable via the central AuditBehavior. No hard delete.

internal static class TrainingAudit
{
    public const string Module = "MOD-0029-FU11";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
    public static AuditRequestMetadata Meta(AuditOperation op, string entityType, Guid entityId, string correlationId) =>
        new(AuditCategory.DocumentManagement, op, entityType, EntityId: entityId, SourceModule: Module, CorrelationId: Correlation(correlationId));
}

public sealed record ResolveTrainingMatrixCommand(Guid RegisterEntryId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TrainingRequirementModel>>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => TrainingAudit.Meta(AuditOperation.Update, "DocumentTrainingMatrix", RegisterEntryId, CorrelationId);
}

public sealed record AddManualTrainingRequirementCommand(Guid RegisterEntryId, AddManualTrainingRequirementInput Input, string CorrelationId)
    : IRequest<Response<TrainingRequirementModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => TrainingAudit.Meta(AuditOperation.Create, "DocumentTrainingRequirement", RegisterEntryId, CorrelationId);
}

public sealed record AssignTrainingCommand(Guid RegisterEntryId, AssignTrainingInput Input, string CorrelationId)
    : IRequest<Response<TrainingAssignmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => TrainingAudit.Meta(AuditOperation.Assign, "DocumentTrainingAssignment", RegisterEntryId, CorrelationId);
}

public sealed record CompleteTrainingCommand(Guid RegisterEntryId, Guid AssignmentId, CompleteTrainingInput Input, string CorrelationId)
    : IRequest<Response<TrainingAssignmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => TrainingAudit.Meta(AuditOperation.Update, "DocumentTrainingAssignment", AssignmentId, CorrelationId);
}

public sealed record RecordTrainingEffectivenessCommand(Guid RegisterEntryId, Guid AssignmentId, RecordEffectivenessInput Input, string CorrelationId)
    : IRequest<Response<TrainingAssignmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => TrainingAudit.Meta(AuditOperation.Update, "DocumentTrainingAssignment", AssignmentId, CorrelationId);
}

public sealed record RestrictTrainingCommand(Guid RegisterEntryId, Guid AssignmentId, RestrictTrainingInput Input, string CorrelationId)
    : IRequest<Response<TrainingAssignmentModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => TrainingAudit.Meta(AuditOperation.Update, "DocumentTrainingAssignment", AssignmentId, CorrelationId);
}
