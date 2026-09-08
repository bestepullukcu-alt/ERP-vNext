using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Features.TaskChecklistEngine.Commands;
using Diten.Platform.Application.Features.TaskChecklistEngine.Models;
using Diten.Platform.Domain.Entities;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.TaskChecklistEngine.Handlers;

internal static class ActorResolver
{
    public static string Resolve(ICurrentUserContext user) =>
        user.IsAuthenticated ? user.UserId.ToString("N") : "system";
}

public sealed class CreateWorkTaskCommandHandler : IRequestHandler<CreateWorkTaskCommand, Response<WorkTaskDetailModel>>
{
    private readonly IWorkTaskRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateWorkTaskCommandHandler(IWorkTaskRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<WorkTaskDetailModel>> Handle(CreateWorkTaskCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return Response<WorkTaskDetailModel>.Fail("title_required", 400);
        }

        var escalation = TaskEscalationPolicy.None;
        if (!string.IsNullOrWhiteSpace(request.EscalationPolicy) &&
            !Enum.TryParse(request.EscalationPolicy.Trim(), true, out escalation))
        {
            return Response<WorkTaskDetailModel>.Fail("invalid_escalation_policy", 400);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var assignee = string.IsNullOrWhiteSpace(request.AssigneeId) ? null : request.AssigneeId.Trim();

        var entity = new WorkTask
        {
            TenantId = Guid.Empty, // stamped by TenantRepository.CreateAsync from tenant context
            WorkTaskId = Guid.NewGuid(),
            Title = request.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = assignee is null ? WorkTaskStatus.Open : WorkTaskStatus.Assigned,
            AssigneeId = assignee,
            DueDate = request.DueDate,
            EscalationPolicy = escalation,
            RequiresEvidence = request.RequiresEvidence,
            CreatedBy = actor
        };

        await _repository.CreateAsync(entity, ct);

        if (assignee is not null)
        {
            await _repository.CreateAssignmentAsync(new TaskAssignment
            {
                TenantId = Guid.Empty,
                TaskAssignmentId = Guid.NewGuid(),
                WorkTaskId = entity.WorkTaskId,
                AssigneeId = assignee,
                AssignedBy = actor,
                AssignedAt = DateTimeOffset.UtcNow,
                IsActive = true,
                CreatedBy = actor
            }, ct);
        }

        return Response<WorkTaskDetailModel>.Success(TaskChecklistMappings.ToDetail(entity), 201);
    }
}

public sealed class AssignWorkTaskCommandHandler : IRequestHandler<AssignWorkTaskCommand, Response<WorkTaskDetailModel>>
{
    private readonly IWorkTaskRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public AssignWorkTaskCommandHandler(IWorkTaskRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<WorkTaskDetailModel>> Handle(AssignWorkTaskCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.AssigneeId))
        {
            return Response<WorkTaskDetailModel>.Fail("assignee_required", 400);
        }

        var entity = await _repository.GetByIdAsync(request.WorkTaskId, ct);
        if (entity is null)
        {
            return Response<WorkTaskDetailModel>.Fail("task_not_found", 404);
        }

        if (entity.Status is WorkTaskStatus.Completed or WorkTaskStatus.Cancelled)
        {
            return Response<WorkTaskDetailModel>.Fail("task_not_assignable", 409);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var expected = entity.RowVersion;
        entity.AssigneeId = request.AssigneeId.Trim();
        entity.Status = WorkTaskStatus.Assigned;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = actor;

        var updated = await _repository.UpdateAsync(entity, expected, ct);
        if (!updated)
        {
            return Response<WorkTaskDetailModel>.Fail("concurrency_conflict", 409);
        }

        await _repository.DeactivateAssignmentsAsync(entity.WorkTaskId, ct);
        await _repository.CreateAssignmentAsync(new TaskAssignment
        {
            TenantId = Guid.Empty,
            TaskAssignmentId = Guid.NewGuid(),
            WorkTaskId = entity.WorkTaskId,
            AssigneeId = entity.AssigneeId!,
            AssignedBy = actor,
            AssignedAt = DateTimeOffset.UtcNow,
            IsActive = true,
            CreatedBy = actor
        }, ct);

        return Response<WorkTaskDetailModel>.Success(TaskChecklistMappings.ToDetail(entity));
    }
}

public sealed class CompleteWorkTaskCommandHandler : IRequestHandler<CompleteWorkTaskCommand, Response<WorkTaskDetailModel>>
{
    private readonly IWorkTaskRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CompleteWorkTaskCommandHandler(IWorkTaskRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<WorkTaskDetailModel>> Handle(CompleteWorkTaskCommand request, CancellationToken ct)
    {
        var entity = await _repository.GetByIdAsync(request.WorkTaskId, ct);
        if (entity is null)
        {
            return Response<WorkTaskDetailModel>.Fail("task_not_found", 404);
        }

        if (entity.Status == WorkTaskStatus.Completed)
        {
            return Response<WorkTaskDetailModel>.Fail("task_already_completed", 409);
        }

        var evidence = string.IsNullOrWhiteSpace(request.EvidenceReference) ? null : request.EvidenceReference.Trim();
        if (entity.RequiresEvidence && evidence is null)
        {
            return Response<WorkTaskDetailModel>.Fail("evidence_required", 422);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var expected = entity.RowVersion;
        entity.Status = WorkTaskStatus.Completed;
        entity.EvidenceReference = evidence;
        entity.CompletedAt = DateTimeOffset.UtcNow;
        entity.CompletedBy = actor;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = actor;

        var updated = await _repository.UpdateAsync(entity, expected, ct);
        return updated
            ? Response<WorkTaskDetailModel>.Success(TaskChecklistMappings.ToDetail(entity))
            : Response<WorkTaskDetailModel>.Fail("concurrency_conflict", 409);
    }
}

public sealed class CreateChecklistTemplateCommandHandler : IRequestHandler<CreateChecklistTemplateCommand, Response<ChecklistTemplateDetailModel>>
{
    private readonly IChecklistRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CreateChecklistTemplateCommandHandler(IChecklistRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<ChecklistTemplateDetailModel>> Handle(CreateChecklistTemplateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Code))
        {
            return Response<ChecklistTemplateDetailModel>.Fail("code_required", 400);
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Response<ChecklistTemplateDetailModel>.Fail("name_required", 400);
        }

        var normalizedCode = request.Code.Trim();
        var existing = await _repository.GetTemplateByCodeAsync(normalizedCode, ct);
        if (existing is not null)
        {
            return Response<ChecklistTemplateDetailModel>.Fail("duplicate_template_code", 409);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var items = (request.Items ?? [])
            .Select((i, index) => new ChecklistTemplateItem
            {
                ItemId = Guid.NewGuid(),
                Title = i.Title.Trim(),
                RequiresEvidence = i.RequiresEvidence,
                Sequence = index
            })
            .ToList();

        var entity = new ChecklistTemplate
        {
            TenantId = Guid.Empty,
            ChecklistTemplateId = Guid.NewGuid(),
            Code = normalizedCode,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Status = ChecklistTemplateStatus.Active,
            Items = items,
            CreatedBy = actor
        };

        await _repository.CreateTemplateAsync(entity, ct);
        return Response<ChecklistTemplateDetailModel>.Success(TaskChecklistMappings.ToDetail(entity), 201);
    }
}

public sealed class UpdateChecklistTemplateCommandHandler : IRequestHandler<UpdateChecklistTemplateCommand, Response<ChecklistTemplateDetailModel>>
{
    private readonly IChecklistRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public UpdateChecklistTemplateCommandHandler(IChecklistRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<ChecklistTemplateDetailModel>> Handle(UpdateChecklistTemplateCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Response<ChecklistTemplateDetailModel>.Fail("name_required", 400);
        }

        var entity = await _repository.GetTemplateByIdAsync(request.TemplateId, ct);
        if (entity is null)
        {
            return Response<ChecklistTemplateDetailModel>.Fail("template_not_found", 404);
        }

        var status = entity.Status;
        if (!string.IsNullOrWhiteSpace(request.Status) &&
            !Enum.TryParse(request.Status.Trim(), true, out status))
        {
            return Response<ChecklistTemplateDetailModel>.Fail("invalid_status", 400);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var expected = request.ExpectedRowVersion;

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.Status = status;
        entity.Items = (request.Items ?? [])
            .Select((i, index) => new ChecklistTemplateItem
            {
                ItemId = Guid.NewGuid(),
                Title = i.Title.Trim(),
                RequiresEvidence = i.RequiresEvidence,
                Sequence = index
            })
            .ToList();
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = actor;

        var updated = await _repository.UpdateTemplateAsync(entity, expected, ct);
        return updated
            ? Response<ChecklistTemplateDetailModel>.Success(TaskChecklistMappings.ToDetail(entity))
            : Response<ChecklistTemplateDetailModel>.Fail("concurrency_conflict", 409);
    }
}

public sealed class StartChecklistRunCommandHandler : IRequestHandler<StartChecklistRunCommand, Response<ChecklistRunDetailModel>>
{
    private readonly IChecklistRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public StartChecklistRunCommandHandler(IChecklistRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<ChecklistRunDetailModel>> Handle(StartChecklistRunCommand request, CancellationToken ct)
    {
        var template = await _repository.GetTemplateByIdAsync(request.TemplateId, ct);
        if (template is null)
        {
            return Response<ChecklistRunDetailModel>.Fail("template_not_found", 404);
        }

        if (template.Status == ChecklistTemplateStatus.Archived)
        {
            return Response<ChecklistRunDetailModel>.Fail("template_archived", 409);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var runItems = template.Items
            .OrderBy(i => i.Sequence)
            .Select(i => new ChecklistRunItem
            {
                ItemId = Guid.NewGuid(),
                Title = i.Title,
                RequiresEvidence = i.RequiresEvidence,
                Sequence = i.Sequence,
                IsCompleted = false
            })
            .ToList();

        var entity = new ChecklistRun
        {
            TenantId = Guid.Empty,
            ChecklistRunId = Guid.NewGuid(),
            TemplateId = template.ChecklistTemplateId,
            TemplateCode = template.Code,
            Name = string.IsNullOrWhiteSpace(request.Name) ? template.Name : request.Name.Trim(),
            Status = ChecklistRunStatus.InProgress,
            Items = runItems,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedBy = actor
        };

        await _repository.CreateRunAsync(entity, ct);
        return Response<ChecklistRunDetailModel>.Success(TaskChecklistMappings.ToDetail(entity), 201);
    }
}

public sealed class CompleteChecklistRunItemCommandHandler : IRequestHandler<CompleteChecklistRunItemCommand, Response<ChecklistRunDetailModel>>
{
    private readonly IChecklistRepository _repository;
    private readonly ICurrentUserContext _currentUser;

    public CompleteChecklistRunItemCommandHandler(IChecklistRepository repository, ICurrentUserContext currentUser)
    {
        _repository = repository;
        _currentUser = currentUser;
    }

    public async Task<Response<ChecklistRunDetailModel>> Handle(CompleteChecklistRunItemCommand request, CancellationToken ct)
    {
        var run = await _repository.GetRunByIdAsync(request.RunId, ct);
        if (run is null)
        {
            return Response<ChecklistRunDetailModel>.Fail("run_not_found", 404);
        }

        if (run.Status != ChecklistRunStatus.InProgress)
        {
            return Response<ChecklistRunDetailModel>.Fail("run_not_in_progress", 409);
        }

        var item = run.Items.FirstOrDefault(i => i.ItemId == request.ItemId);
        if (item is null)
        {
            return Response<ChecklistRunDetailModel>.Fail("run_item_not_found", 404);
        }

        var evidence = string.IsNullOrWhiteSpace(request.EvidenceReference) ? null : request.EvidenceReference.Trim();
        if (item.RequiresEvidence && evidence is null)
        {
            return Response<ChecklistRunDetailModel>.Fail("evidence_required", 422);
        }

        var actor = ActorResolver.Resolve(_currentUser);
        var expected = run.RowVersion;

        item.IsCompleted = true;
        item.EvidenceReference = evidence;
        item.CompletedAt = DateTimeOffset.UtcNow;
        item.CompletedBy = actor;

        if (run.Items.All(i => i.IsCompleted))
        {
            run.Status = ChecklistRunStatus.Completed;
            run.CompletedAt = DateTimeOffset.UtcNow;
        }

        run.UpdatedAt = DateTimeOffset.UtcNow;
        run.UpdatedBy = actor;

        var updated = await _repository.UpdateRunAsync(run, expected, ct);
        return updated
            ? Response<ChecklistRunDetailModel>.Success(TaskChecklistMappings.ToDetail(run))
            : Response<ChecklistRunDetailModel>.Fail("concurrency_conflict", 409);
    }
}
