using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.WorkflowDesigner.Commands;
using Diten.Platform.Application.Features.WorkflowDesigner.Models;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.WorkflowDesigner.Handlers;

internal static class WorkflowHelpers
{
    public static string Actor(ICurrentUserContext user) => user.IsAuthenticated ? user.UserId.ToString("N") : "system";

    public static void AddTimeline(WorkflowInstance inst, string kind, int stepSeq, string actor, string? note = null) =>
        inst.Timeline.Add(new WorkflowTimelineEntry
        {
            Sequence = inst.Timeline.Count,
            Kind = kind,
            StepSequence = stepSeq,
            Actor = actor,
            At = DateTimeOffset.UtcNow,
            Note = note
        });

    public static ApprovalTask BuildTask(WorkflowInstance inst, WorkflowInstanceStep step, string actor) => new()
    {
        TenantId = Guid.Empty,
        ApprovalTaskId = Guid.NewGuid(),
        WorkflowInstanceId = inst.WorkflowInstanceId,
        DefinitionCode = inst.DefinitionCode,
        StepId = step.StepId,
        StepSequence = step.Sequence,
        StepName = step.Name,
        AssigneeRole = step.ApproverRole,
        Status = ApprovalTaskStatus.Pending,
        RequiresEvidence = step.RequiresEvidence,
        DueAt = step.SlaHours.HasValue ? DateTimeOffset.UtcNow.AddHours(step.SlaHours.Value) : null,
        EscalationAction = step.EscalationAction,
        CreatedBy = actor
    };
}

public sealed class CreateWorkflowDefinitionCommandHandler : IRequestHandler<CreateWorkflowDefinitionCommand, Response<WorkflowDefinitionDetailModel>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    public CreateWorkflowDefinitionCommandHandler(IWorkflowDefinitionRepository repository, ICurrentUserContext currentUser)
    { _repository = repository; _currentUser = currentUser; }

    public async Task<Response<WorkflowDefinitionDetailModel>> Handle(CreateWorkflowDefinitionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) return Response<WorkflowDefinitionDetailModel>.Fail("code_required", 400);
        if (string.IsNullOrWhiteSpace(request.Name)) return Response<WorkflowDefinitionDetailModel>.Fail("name_required", 400);

        var code = request.Code.Trim();
        if (await _repository.GetLatestVersionNumberAsync(code, ct) > 0)
            return Response<WorkflowDefinitionDetailModel>.Fail("duplicate_definition_code", 409);

        var (steps, slaRules, error) = BuildStepsAndRules(request.Steps, request.SlaRules);
        if (error is not null) return Response<WorkflowDefinitionDetailModel>.Fail(error, 400);

        var entity = new WorkflowDefinition
        {
            TenantId = Guid.Empty,
            WorkflowDefinitionId = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            VersionNumber = 1,
            Status = WorkflowDefinitionStatus.Draft,
            Steps = steps,
            SlaRules = slaRules,
            CreatedBy = WorkflowHelpers.Actor(_currentUser)
        };
        await _repository.CreateAsync(entity, ct);
        return Response<WorkflowDefinitionDetailModel>.Success(WorkflowMappings.ToDetail(entity), 201);
    }

    internal static (List<WorkflowStep> Steps, List<SlaEscalationRule> Rules, string? Error) BuildStepsAndRules(
        IReadOnlyList<WorkflowStepInput> stepInputs, IReadOnlyList<SlaRuleInput> ruleInputs)
    {
        var steps = new List<WorkflowStep>();
        var seq = 0;
        foreach (var s in stepInputs ?? [])
        {
            if (string.IsNullOrWhiteSpace(s.Name)) return ([], [], "step_name_required");
            steps.Add(new WorkflowStep
            {
                StepId = Guid.NewGuid(),
                Name = s.Name.Trim(),
                Sequence = seq++,
                ApproverRole = string.IsNullOrWhiteSpace(s.ApproverRole) ? null : s.ApproverRole.Trim(),
                RequiresEvidence = s.RequiresEvidence
            });
        }

        var rules = new List<SlaEscalationRule>();
        foreach (var r in ruleInputs ?? [])
        {
            var action = WorkflowEscalationAction.None;
            if (!string.IsNullOrWhiteSpace(r.EscalationAction) && !Enum.TryParse(r.EscalationAction.Trim(), true, out action))
                return ([], [], "invalid_escalation_action");
            rules.Add(new SlaEscalationRule
            {
                RuleId = Guid.NewGuid(),
                StepSequence = r.StepSequence,
                SlaHours = r.SlaHours < 0 ? 0 : r.SlaHours,
                EscalationAction = action,
                EscalateToRole = string.IsNullOrWhiteSpace(r.EscalateToRole) ? null : r.EscalateToRole.Trim()
            });
        }
        return (steps, rules, null);
    }
}

public sealed class UpdateWorkflowDefinitionCommandHandler : IRequestHandler<UpdateWorkflowDefinitionCommand, Response<WorkflowDefinitionDetailModel>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    public UpdateWorkflowDefinitionCommandHandler(IWorkflowDefinitionRepository repository, ICurrentUserContext currentUser)
    { _repository = repository; _currentUser = currentUser; }

    public async Task<Response<WorkflowDefinitionDetailModel>> Handle(UpdateWorkflowDefinitionCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) return Response<WorkflowDefinitionDetailModel>.Fail("name_required", 400);

        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity is null) return Response<WorkflowDefinitionDetailModel>.Fail("definition_not_found", 404);
        if (entity.Status != WorkflowDefinitionStatus.Draft) return Response<WorkflowDefinitionDetailModel>.Fail("definition_not_editable", 409);

        var (steps, slaRules, error) = CreateWorkflowDefinitionCommandHandler.BuildStepsAndRules(request.Steps, request.SlaRules);
        if (error is not null) return Response<WorkflowDefinitionDetailModel>.Fail(error, 400);

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Steps = steps;
        entity.SlaRules = slaRules;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = WorkflowHelpers.Actor(_currentUser);

        var updated = await _repository.UpdateAsync(entity, request.ExpectedRowVersion, ct);
        return updated
            ? Response<WorkflowDefinitionDetailModel>.Success(WorkflowMappings.ToDetail(entity))
            : Response<WorkflowDefinitionDetailModel>.Fail("concurrency_conflict", 409);
    }
}

public sealed class CreateWorkflowDefinitionVersionCommandHandler : IRequestHandler<CreateWorkflowDefinitionVersionCommand, Response<WorkflowDefinitionDetailModel>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    public CreateWorkflowDefinitionVersionCommandHandler(IWorkflowDefinitionRepository repository, ICurrentUserContext currentUser)
    { _repository = repository; _currentUser = currentUser; }

    public async Task<Response<WorkflowDefinitionDetailModel>> Handle(CreateWorkflowDefinitionVersionCommand request, CancellationToken ct)
    {
        var code = (request.Code ?? string.Empty).Trim();
        var latest = await _repository.GetLatestVersionNumberAsync(code, ct);
        if (latest == 0) return Response<WorkflowDefinitionDetailModel>.Fail("definition_not_found", 404);

        var source = await _repository.GetByCodeVersionAsync(code, latest, ct);
        if (source is null) return Response<WorkflowDefinitionDetailModel>.Fail("definition_not_found", 404);

        var entity = new WorkflowDefinition
        {
            TenantId = Guid.Empty,
            WorkflowDefinitionId = Guid.NewGuid(),
            Code = code,
            Name = source.Name,
            Description = source.Description,
            VersionNumber = latest + 1,
            Status = WorkflowDefinitionStatus.Draft,
            Steps = source.Steps.OrderBy(s => s.Sequence).Select(s => new WorkflowStep
            {
                StepId = Guid.NewGuid(), Name = s.Name, Sequence = s.Sequence, ApproverRole = s.ApproverRole, RequiresEvidence = s.RequiresEvidence
            }).ToList(),
            SlaRules = source.SlaRules.Select(r => new SlaEscalationRule
            {
                RuleId = Guid.NewGuid(), StepSequence = r.StepSequence, SlaHours = r.SlaHours, EscalationAction = r.EscalationAction, EscalateToRole = r.EscalateToRole
            }).ToList(),
            CreatedBy = WorkflowHelpers.Actor(_currentUser)
        };
        await _repository.CreateAsync(entity, ct);
        return Response<WorkflowDefinitionDetailModel>.Success(WorkflowMappings.ToDetail(entity), 201);
    }
}

public sealed class PublishWorkflowDefinitionCommandHandler : IRequestHandler<PublishWorkflowDefinitionCommand, Response<WorkflowDefinitionDetailModel>>
{
    private readonly IWorkflowDefinitionRepository _repository;
    private readonly ICurrentUserContext _currentUser;
    public PublishWorkflowDefinitionCommandHandler(IWorkflowDefinitionRepository repository, ICurrentUserContext currentUser)
    { _repository = repository; _currentUser = currentUser; }

    public async Task<Response<WorkflowDefinitionDetailModel>> Handle(PublishWorkflowDefinitionCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.Id, ct);
        if (entity is null) return Response<WorkflowDefinitionDetailModel>.Fail("definition_not_found", 404);
        if (entity.Status != WorkflowDefinitionStatus.Draft) return Response<WorkflowDefinitionDetailModel>.Fail("definition_not_draft", 409);
        if (entity.Steps.Count == 0) return Response<WorkflowDefinitionDetailModel>.Fail("definition_has_no_steps", 400);

        var actor = WorkflowHelpers.Actor(_currentUser);

        // Archive the currently-published version of the same code, if any (explicit single active version).
        var currentlyPublished = await _repository.GetPublishedByCodeAsync(entity.Code, ct);
        if (currentlyPublished is not null && currentlyPublished.WorkflowDefinitionId != entity.WorkflowDefinitionId)
        {
            currentlyPublished.Status = WorkflowDefinitionStatus.Archived;
            currentlyPublished.UpdatedAt = DateTimeOffset.UtcNow;
            currentlyPublished.UpdatedBy = actor;
            await _repository.UpdateAsync(currentlyPublished, currentlyPublished.RowVersion, ct);
        }

        entity.Status = WorkflowDefinitionStatus.Published;
        entity.PublishedAt = DateTimeOffset.UtcNow;
        entity.PublishedBy = actor;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = actor;

        var updated = await _repository.UpdateAsync(entity, request.ExpectedRowVersion, ct);
        return updated
            ? Response<WorkflowDefinitionDetailModel>.Success(WorkflowMappings.ToDetail(entity))
            : Response<WorkflowDefinitionDetailModel>.Fail("concurrency_conflict", 409);
    }
}

public sealed class StartWorkflowInstanceCommandHandler : IRequestHandler<StartWorkflowInstanceCommand, Response<WorkflowInstanceDetailModel>>
{
    private readonly IWorkflowDefinitionRepository _definitions;
    private readonly IWorkflowRuntimeRepository _runtime;
    private readonly ICurrentUserContext _currentUser;
    public StartWorkflowInstanceCommandHandler(IWorkflowDefinitionRepository definitions, IWorkflowRuntimeRepository runtime, ICurrentUserContext currentUser)
    { _definitions = definitions; _runtime = runtime; _currentUser = currentUser; }

    public async Task<Response<WorkflowInstanceDetailModel>> Handle(StartWorkflowInstanceCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.SubjectReference))
            return Response<WorkflowInstanceDetailModel>.Fail("subject_reference_required", 400);

        var code = (request.DefinitionCode ?? string.Empty).Trim();
        var def = await _definitions.GetPublishedByCodeAsync(code, ct);
        if (def is null) return Response<WorkflowInstanceDetailModel>.Fail("no_published_definition", 404);
        if (def.Steps.Count == 0) return Response<WorkflowInstanceDetailModel>.Fail("definition_has_no_steps", 400);

        var actor = WorkflowHelpers.Actor(_currentUser);
        var orderedSteps = def.Steps.OrderBy(s => s.Sequence).ToList();
        var snapshot = orderedSteps.Select(s =>
        {
            var rule = def.SlaRules.FirstOrDefault(r => r.StepSequence == s.Sequence);
            return new WorkflowInstanceStep
            {
                StepId = s.StepId,
                Name = s.Name,
                Sequence = s.Sequence,
                ApproverRole = s.ApproverRole,
                RequiresEvidence = s.RequiresEvidence,
                SlaHours = rule?.SlaHours,
                EscalationAction = rule?.EscalationAction ?? WorkflowEscalationAction.None,
                EscalateToRole = rule?.EscalateToRole
            };
        }).ToList();

        var firstStep = snapshot[0];
        var instance = new WorkflowInstance
        {
            TenantId = Guid.Empty,
            WorkflowInstanceId = Guid.NewGuid(),
            DefinitionId = def.WorkflowDefinitionId,
            DefinitionCode = def.Code,
            DefinitionVersion = def.VersionNumber,
            SubjectReference = request.SubjectReference.Trim(),
            Status = WorkflowInstanceStatus.Running,
            CurrentStepSequence = firstStep.Sequence,
            Steps = snapshot,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor
        };
        WorkflowHelpers.AddTimeline(instance, "started", firstStep.Sequence, actor);

        await _runtime.CreateInstanceAsync(instance, ct);
        await _runtime.CreateTaskAsync(WorkflowHelpers.BuildTask(instance, firstStep, actor), ct);

        return Response<WorkflowInstanceDetailModel>.Success(WorkflowMappings.ToDetail(instance), 201);
    }
}

public sealed class ApproveApprovalTaskCommandHandler : IRequestHandler<ApproveApprovalTaskCommand, Response<WorkflowInstanceDetailModel>>
{
    private readonly IWorkflowRuntimeRepository _runtime;
    private readonly ICurrentUserContext _currentUser;
    public ApproveApprovalTaskCommandHandler(IWorkflowRuntimeRepository runtime, ICurrentUserContext currentUser)
    { _runtime = runtime; _currentUser = currentUser; }

    public async Task<Response<WorkflowInstanceDetailModel>> Handle(ApproveApprovalTaskCommand request, CancellationToken ct)
    {
        var task = await _runtime.GetTaskByIdAsync(request.TaskId, ct);
        if (task is null) return Response<WorkflowInstanceDetailModel>.Fail("task_not_found", 404);
        if (task.Status != ApprovalTaskStatus.Pending) return Response<WorkflowInstanceDetailModel>.Fail("task_not_pending", 409);

        var evidence = string.IsNullOrWhiteSpace(request.EvidenceReference) ? null : request.EvidenceReference.Trim();
        if (task.RequiresEvidence && evidence is null) return Response<WorkflowInstanceDetailModel>.Fail("evidence_required", 422);

        var instance = await _runtime.GetInstanceByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null) return Response<WorkflowInstanceDetailModel>.Fail("instance_not_found", 404);
        if (instance.Status != WorkflowInstanceStatus.Running) return Response<WorkflowInstanceDetailModel>.Fail("instance_not_running", 409);

        var actor = WorkflowHelpers.Actor(_currentUser);

        var taskExpected = task.RowVersion;
        task.Status = ApprovalTaskStatus.Approved;
        task.Decision = "approved";
        task.DecisionBy = actor;
        task.DecisionAt = DateTimeOffset.UtcNow;
        task.EvidenceReference = evidence;
        task.Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.UpdatedBy = actor;
        if (!await _runtime.UpdateTaskAsync(task, taskExpected, ct))
            return Response<WorkflowInstanceDetailModel>.Fail("concurrency_conflict", 409);

        var instExpected = instance.RowVersion;
        WorkflowHelpers.AddTimeline(instance, "step_approved", task.StepSequence, actor, task.Note);

        var nextStep = instance.Steps.Where(s => s.Sequence > task.StepSequence).OrderBy(s => s.Sequence).FirstOrDefault();
        if (nextStep is not null)
        {
            instance.CurrentStepSequence = nextStep.Sequence;
        }
        else
        {
            instance.Status = WorkflowInstanceStatus.Completed;
            instance.CompletedAt = DateTimeOffset.UtcNow;
            WorkflowHelpers.AddTimeline(instance, "completed", task.StepSequence, actor);
        }

        instance.UpdatedAt = DateTimeOffset.UtcNow;
        instance.UpdatedBy = actor;
        if (!await _runtime.UpdateInstanceAsync(instance, instExpected, ct))
            return Response<WorkflowInstanceDetailModel>.Fail("concurrency_conflict", 409);

        if (nextStep is not null)
            await _runtime.CreateTaskAsync(WorkflowHelpers.BuildTask(instance, nextStep, actor), ct);

        return Response<WorkflowInstanceDetailModel>.Success(WorkflowMappings.ToDetail(instance));
    }
}

public sealed class RejectApprovalTaskCommandHandler : IRequestHandler<RejectApprovalTaskCommand, Response<WorkflowInstanceDetailModel>>
{
    private readonly IWorkflowRuntimeRepository _runtime;
    private readonly ICurrentUserContext _currentUser;
    public RejectApprovalTaskCommandHandler(IWorkflowRuntimeRepository runtime, ICurrentUserContext currentUser)
    { _runtime = runtime; _currentUser = currentUser; }

    public async Task<Response<WorkflowInstanceDetailModel>> Handle(RejectApprovalTaskCommand request, CancellationToken ct)
    {
        var task = await _runtime.GetTaskByIdAsync(request.TaskId, ct);
        if (task is null) return Response<WorkflowInstanceDetailModel>.Fail("task_not_found", 404);
        if (task.Status != ApprovalTaskStatus.Pending) return Response<WorkflowInstanceDetailModel>.Fail("task_not_pending", 409);

        var instance = await _runtime.GetInstanceByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is null) return Response<WorkflowInstanceDetailModel>.Fail("instance_not_found", 404);
        if (instance.Status != WorkflowInstanceStatus.Running) return Response<WorkflowInstanceDetailModel>.Fail("instance_not_running", 409);

        var actor = WorkflowHelpers.Actor(_currentUser);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var taskExpected = task.RowVersion;
        task.Status = ApprovalTaskStatus.Rejected;
        task.Decision = "rejected";
        task.DecisionBy = actor;
        task.DecisionAt = DateTimeOffset.UtcNow;
        task.Note = note;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.UpdatedBy = actor;
        if (!await _runtime.UpdateTaskAsync(task, taskExpected, ct))
            return Response<WorkflowInstanceDetailModel>.Fail("concurrency_conflict", 409);

        var instExpected = instance.RowVersion;
        WorkflowHelpers.AddTimeline(instance, "step_rejected", task.StepSequence, actor, note);
        instance.Status = WorkflowInstanceStatus.Rejected;
        instance.CompletedAt = DateTimeOffset.UtcNow;
        WorkflowHelpers.AddTimeline(instance, "rejected", task.StepSequence, actor);
        instance.UpdatedAt = DateTimeOffset.UtcNow;
        instance.UpdatedBy = actor;
        if (!await _runtime.UpdateInstanceAsync(instance, instExpected, ct))
            return Response<WorkflowInstanceDetailModel>.Fail("concurrency_conflict", 409);

        return Response<WorkflowInstanceDetailModel>.Success(WorkflowMappings.ToDetail(instance));
    }
}

public sealed class DelegateApprovalTaskCommandHandler : IRequestHandler<DelegateApprovalTaskCommand, Response<ApprovalTaskDetailModel>>
{
    private readonly IWorkflowRuntimeRepository _runtime;
    private readonly ICurrentUserContext _currentUser;
    public DelegateApprovalTaskCommandHandler(IWorkflowRuntimeRepository runtime, ICurrentUserContext currentUser)
    { _runtime = runtime; _currentUser = currentUser; }

    public async Task<Response<ApprovalTaskDetailModel>> Handle(DelegateApprovalTaskCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ToAssigneeId)) return Response<ApprovalTaskDetailModel>.Fail("assignee_required", 400);

        var task = await _runtime.GetTaskByIdAsync(request.TaskId, ct);
        if (task is null) return Response<ApprovalTaskDetailModel>.Fail("task_not_found", 404);
        if (task.Status != ApprovalTaskStatus.Pending) return Response<ApprovalTaskDetailModel>.Fail("task_not_pending", 409);

        var actor = WorkflowHelpers.Actor(_currentUser);
        var note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();

        var taskExpected = task.RowVersion;
        task.AssigneeId = request.ToAssigneeId.Trim();
        task.Note = note;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.UpdatedBy = actor;
        if (!await _runtime.UpdateTaskAsync(task, taskExpected, ct))
            return Response<ApprovalTaskDetailModel>.Fail("concurrency_conflict", 409);

        var instance = await _runtime.GetInstanceByIdAsync(task.WorkflowInstanceId, ct);
        if (instance is not null && instance.Status == WorkflowInstanceStatus.Running)
        {
            WorkflowHelpers.AddTimeline(instance, "delegated", task.StepSequence, actor, $"→ {task.AssigneeId}");
            instance.UpdatedAt = DateTimeOffset.UtcNow;
            instance.UpdatedBy = actor;
            await _runtime.UpdateInstanceAsync(instance, instance.RowVersion, ct);
        }

        return Response<ApprovalTaskDetailModel>.Success(WorkflowMappings.ToDetail(task));
    }
}
