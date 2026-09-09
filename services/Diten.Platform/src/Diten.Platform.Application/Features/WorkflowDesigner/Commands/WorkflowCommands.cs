using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.WorkflowDesigner.Models;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.WorkflowDesigner.Commands;

public sealed record WorkflowStepInput(string Name, string? ApproverRole, bool RequiresEvidence);
public sealed record SlaRuleInput(int StepSequence, int SlaHours, string? EscalationAction, string? EscalateToRole);

public sealed record CreateWorkflowDefinitionCommand(
    string Code, string Name, string? Description,
    IReadOnlyList<WorkflowStepInput> Steps, IReadOnlyList<SlaRuleInput> SlaRules)
    : IRequest<Response<WorkflowDefinitionDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration, AuditOperation.Create, "WorkflowDefinition",
        SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["code"] = Code });
}

public sealed record UpdateWorkflowDefinitionCommand(
    Guid Id, string Name, string? Description,
    IReadOnlyList<WorkflowStepInput> Steps, IReadOnlyList<SlaRuleInput> SlaRules, long ExpectedRowVersion)
    : IRequest<Response<WorkflowDefinitionDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration, AuditOperation.Update, "WorkflowDefinition",
        EntityId: Id, SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId);
}

public sealed record CreateWorkflowDefinitionVersionCommand(string Code)
    : IRequest<Response<WorkflowDefinitionDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration, AuditOperation.Create, "WorkflowDefinition",
        SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["code"] = Code, ["kind"] = "new_version" });
}

public sealed record PublishWorkflowDefinitionCommand(Guid Id, long ExpectedRowVersion)
    : IRequest<Response<WorkflowDefinitionDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration, AuditOperation.Update, "WorkflowDefinition",
        EntityId: Id, SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["lifecycle"] = "workflow.definition.published" });
}

public sealed record StartWorkflowInstanceCommand(string DefinitionCode, string SubjectReference)
    : IRequest<Response<WorkflowInstanceDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Execute, "WorkflowInstance",
        SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["definitionCode"] = DefinitionCode, ["lifecycle"] = "workflow.instance.started" });
}

public sealed record ApproveApprovalTaskCommand(Guid TaskId, string? EvidenceReference, string? Note)
    : IRequest<Response<WorkflowInstanceDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Execute, "ApprovalTask",
        EntityId: TaskId, SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["decision"] = "approved", ["lifecycle"] = "workflow.task.completed" });
}

public sealed record RejectApprovalTaskCommand(Guid TaskId, string? Note)
    : IRequest<Response<WorkflowInstanceDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Execute, "ApprovalTask",
        EntityId: TaskId, SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["decision"] = "rejected", ["lifecycle"] = "workflow.task.completed" });
}

public sealed record DelegateApprovalTaskCommand(Guid TaskId, string ToAssigneeId, string? Note)
    : IRequest<Response<ApprovalTaskDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Assign, "ApprovalTask",
        EntityId: TaskId, SourceModule: "MOD-0023", IsPlatformGlobal: true, TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["decision"] = "delegated", ["toAssigneeId"] = ToAssigneeId });
}
