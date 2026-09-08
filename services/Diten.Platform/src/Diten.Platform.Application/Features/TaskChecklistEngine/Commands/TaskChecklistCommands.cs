using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Application.Features.TaskChecklistEngine.Models;
using Diten.Platform.Domain.Entities.Audit;
using Diten.Platform.Domain.Enums;
using MediatR;

namespace Diten.Platform.Application.Features.TaskChecklistEngine.Commands;

public sealed record ChecklistTemplateItemInput(string Title, bool RequiresEvidence);

public sealed record CreateWorkTaskCommand(
    string Title,
    string? Description,
    string? AssigneeId,
    DateTimeOffset? DueDate,
    string? EscalationPolicy,
    bool RequiresEvidence)
    : IRequest<Response<WorkTaskDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Create, "PlatformTask",
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["title"] = Title, ["lifecycle"] = "task.created" });
}

public sealed record AssignWorkTaskCommand(Guid WorkTaskId, string AssigneeId)
    : IRequest<Response<WorkTaskDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Assign, "PlatformTask",
        EntityId: WorkTaskId,
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["assigneeId"] = AssigneeId });
}

public sealed record CompleteWorkTaskCommand(Guid WorkTaskId, string? EvidenceReference)
    : IRequest<Response<WorkTaskDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Execute, "PlatformTask",
        EntityId: WorkTaskId,
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["lifecycle"] = "task.completed" });
}

public sealed record CreateChecklistTemplateCommand(
    string Code,
    string Name,
    string? Description,
    IReadOnlyList<ChecklistTemplateItemInput> Items)
    : IRequest<Response<ChecklistTemplateDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration, AuditOperation.Create, "ChecklistTemplate",
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["code"] = Code });
}

public sealed record UpdateChecklistTemplateCommand(
    Guid TemplateId,
    string Name,
    string? Description,
    string? Status,
    IReadOnlyList<ChecklistTemplateItemInput> Items,
    long ExpectedRowVersion)
    : IRequest<Response<ChecklistTemplateDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.PlatformConfiguration, AuditOperation.Update, "ChecklistTemplate",
        EntityId: TemplateId,
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId);
}

public sealed record StartChecklistRunCommand(Guid TemplateId, string? Name)
    : IRequest<Response<ChecklistRunDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Execute, "ChecklistRun",
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["templateId"] = TemplateId });
}

public sealed record CompleteChecklistRunItemCommand(Guid RunId, Guid ItemId, string? EvidenceReference)
    : IRequest<Response<ChecklistRunDetailModel>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.System, AuditOperation.Execute, "ChecklistRunItem",
        EntityId: RunId,
        SourceModule: "MOD-0024",
        IsPlatformGlobal: true,
        TargetTenantId: AuditTenantIds.PlatformSystemTenantId,
        Metadata: new Dictionary<string, object?> { ["itemId"] = ItemId });
}
