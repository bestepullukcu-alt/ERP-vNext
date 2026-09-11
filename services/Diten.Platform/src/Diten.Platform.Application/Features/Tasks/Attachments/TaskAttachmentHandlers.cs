using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts;
using Diten.Platform.Application.Contracts.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository;
using Diten.Platform.Application.Features.DocumentRepository.Services;
using Diten.Platform.Common.Tenancy;
using Diten.Platform.Domain.Entities.Tasks;
using Diten.Platform.Domain.Enums.Tasks;
using Diten.Platform.Domain.Repositories;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Attachments;

/// <summary>
/// MOD-0024 Slice ATT-1 — shared authorization the four attachment handlers all need: the task exists (tenant
/// scope is the cross-tenant guard, same as every other Tasks handler), the actor is the holder or the
/// requester, and — for a write — the task is not closed.
/// </summary>
internal static class TaskAttachmentAuthorization
{
    public static async Task<(TaskItem? Task, Response<T>? Failure)> ResolveAsync<T>(
        ITaskItemRepository tasks, Guid taskItemId, Guid actorId, string correlationId,
        CancellationToken ct, bool requireOpen)
    {
        var task = await tasks.GetByIdAsync(taskItemId, ct);
        if (task is null)
        {
            return (null, Response<T>.Fail("Task not found.", 404, TaskReasonCodes.NotFound, correlationId));
        }

        // Holder or requester — nobody else's act (WP AC3). Neither AssigneeUserId nor CreatedByUserId being the
        // actor is refused with the SAME 403 either way; distinguishing "not the holder" from "not the requester"
        // would tell a probing caller something about the task's assignment they are not entitled to.
        if (task.AssigneeUserId != actorId && task.CreatedByUserId != actorId)
        {
            return (null, Response<T>.Fail(
                "Only the task's holder or requester may manage its attachments.",
                403, TaskReasonCodes.AttachmentNotAuthorized, correlationId));
        }

        if (requireOpen && task.Lifecycle is TaskLifecycle.Done or TaskLifecycle.Cancelled)
        {
            return (null, Response<T>.Fail(
                "A closed task's attachments cannot be changed.",
                409, TaskReasonCodes.AttachmentTaskClosed, correlationId));
        }

        return (task, null);
    }
}

/// <summary>
/// Uploads one file. Validates the checklist item (if named) BEFORE the file is stored — a rejected upload must
/// not leave an orphan object in the repository for a code path that was always going to fail.
/// </summary>
public sealed class AddTaskAttachmentHandler(
    ITaskItemRepository tasks,
    IChecklistRunRepository checklistRuns,
    ITaskAttachmentRepository attachments,
    DocumentRepositoryService documentRepository,
    ITenantContext tenantContext,
    ICurrentUserContext currentUser)
    : IRequestHandler<AddTaskAttachmentCommand, Response<TaskAttachmentDto>>
{
    private const int NoteMaxLength = 500;

    public async Task<Response<TaskAttachmentDto>> Handle(AddTaskAttachmentCommand request, CancellationToken ct)
    {
        var tenantId = TenantGuard.RequireTenant(tenantContext);
        var actorId = currentUser.UserId;

        var (task, failure) = await TaskAttachmentAuthorization.ResolveAsync<TaskAttachmentDto>(
            tasks, request.TaskItemId, actorId, request.CorrelationId, ct, requireOpen: true);
        if (failure is not null) { return failure; }

        if (request.Note is { Length: > NoteMaxLength })
        {
            return Response<TaskAttachmentDto>.Fail(
                $"A note may be at most {NoteMaxLength} characters.",
                400, TaskReasonCodes.ValidationFailed, request.CorrelationId);
        }

        if (request.ChecklistItemCode is { Length: > 0 } code)
        {
            var run = await checklistRuns.GetByTaskIdAsync(request.TaskItemId, ct);
            if (run is null || !run.Items.Any(i => i.Code == code))
            {
                return Response<TaskAttachmentDto>.Fail(
                    "Checklist item not found.", 404, TaskReasonCodes.AttachmentChecklistItemNotFound, request.CorrelationId);
            }
        }

        // CompanyId: Tasks are not company-partitioned data in this pack (unlike MOD-0029's documents) — Guid.Empty
        // is the honest "not applicable" value, not a placeholder standing in for a lookup this slice does not do.
        var stored = await documentRepository.StoreAsync(
            new StoreRepositoryObjectInput(
                ContentStorageScope.TaskAttachments,
                Guid.Empty,
                request.TaskItemId,
                // Each upload is its OWN artifact, not a revision of a prior one — Tasks has no versioning concept
                // for attachments, so a fresh id per upload is the correct mapping onto the repository's
                // per-version object-key segment (see TaskAttachment.cs's own remark on why ContentRef is not
                // embedded here).
                Guid.NewGuid(),
                request.FileName,
                request.MediaType,
                request.Content),
            request.CorrelationId,
            ct);

        if (!stored.IsSuccessful || stored.Data is null)
        {
            return Response<TaskAttachmentDto>.Fail(
                stored.Errors, stored.StatusCode == 0 ? 503 : stored.StatusCode, stored.ReasonCode, request.CorrelationId);
        }

        var result = stored.Data;
        var now = DateTimeOffset.UtcNow;
        var entity = new TaskAttachment
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskId = request.TaskItemId,
            ChecklistRunItemCode = request.ChecklistItemCode,
            Kind = request.Kind,
            ContentId = result.ContentId,
            FileName = result.FileName,
            MediaType = result.MediaType,
            ByteSize = result.ByteSize,
            Checksum = result.Checksum,
            Note = request.Note?.Trim(),
            UploadedByUserId = actorId,
            UploadedAt = now,
            CreatedBy = currentUser.ActorName
        };

        try
        {
            await attachments.CreateAsync(entity, ct);
        }
        catch (Exception)
        {
            // No metadata orphan: the bytes landed but our own row did not, so compensate through the SAME
            // repository that stored it — this is exactly the pattern DocumentRepositoryService.StoreAsync uses
            // internally for its own RepositoryObject row, applied here for TaskAttachment's row.
            await documentRepository.CompensateAsync(result.ContentId, request.CorrelationId, CancellationToken.None);
            throw;
        }

        return Response<TaskAttachmentDto>.Success(ToDto(entity), 201, request.CorrelationId);
    }

    private static TaskAttachmentDto ToDto(TaskAttachment a) => new(
        a.Id, a.TaskId, a.ChecklistRunItemCode, a.Kind, a.FileName, a.MediaType, a.ByteSize,
        a.Note, a.UploadedByUserId, a.UploadedAt);
}

/// <summary>Soft delete. The stored object is never removed (AD-6) — this is a record of what was once attached.</summary>
public sealed class RemoveTaskAttachmentHandler(
    ITaskItemRepository tasks,
    ITaskAttachmentRepository attachments,
    ICurrentUserContext currentUser)
    : IRequestHandler<RemoveTaskAttachmentCommand, Response<NoContent>>
{
    public async Task<Response<NoContent>> Handle(RemoveTaskAttachmentCommand request, CancellationToken ct)
    {
        var actorId = currentUser.UserId;
        var (_, failure) = await TaskAttachmentAuthorization.ResolveAsync<NoContent>(
            tasks, request.TaskItemId, actorId, request.CorrelationId, ct, requireOpen: true);
        if (failure is not null) { return failure; }

        var attachment = await attachments.GetByIdAsync(request.AttachmentId, ct);
        if (attachment is null || attachment.TaskId != request.TaskItemId)
        {
            // Another task's attachment id lands here too — 404, never a leak of "that id exists elsewhere".
            return Response<NoContent>.Fail(
                "Attachment not found.", 404, TaskReasonCodes.AttachmentNotFound, request.CorrelationId);
        }

        await attachments.SoftDeleteAsync(attachment.Id, currentUser.ActorName, ct);
        return Response<NoContent>.Success(204, request.CorrelationId);
    }
}

public sealed class ListTaskAttachmentsHandler(ITaskItemRepository tasks, ITaskAttachmentRepository attachments)
    : IRequestHandler<ListTaskAttachmentsQuery, Response<IReadOnlyList<TaskAttachmentDto>>>
{
    public async Task<Response<IReadOnlyList<TaskAttachmentDto>>> Handle(
        ListTaskAttachmentsQuery request, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(request.TaskItemId, ct);
        if (task is null)
        {
            return Response<IReadOnlyList<TaskAttachmentDto>>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, request.CorrelationId);
        }

        var rows = await attachments.ListByTaskIdAsync(request.TaskItemId, ct);
        return Response<IReadOnlyList<TaskAttachmentDto>>.Success(
            rows.Select(a => new TaskAttachmentDto(
                a.Id, a.TaskId, a.ChecklistRunItemCode, a.Kind, a.FileName, a.MediaType, a.ByteSize,
                a.Note, a.UploadedByUserId, a.UploadedAt)).ToList(),
            200, request.CorrelationId);
    }
}

public sealed class OpenTaskAttachmentHandler(
    ITaskItemRepository tasks, ITaskAttachmentRepository attachments, DocumentRepositoryService documentRepository)
    : IRequestHandler<OpenTaskAttachmentQuery, Response<TaskAttachmentContentHandle>>
{
    public async Task<Response<TaskAttachmentContentHandle>> Handle(OpenTaskAttachmentQuery request, CancellationToken ct)
    {
        var task = await tasks.GetByIdAsync(request.TaskItemId, ct);
        if (task is null)
        {
            return Response<TaskAttachmentContentHandle>.Fail(
                "Task not found.", 404, TaskReasonCodes.NotFound, request.CorrelationId);
        }

        var attachment = await attachments.GetByIdAsync(request.AttachmentId, ct);
        if (attachment is null || attachment.TaskId != request.TaskItemId)
        {
            return Response<TaskAttachmentContentHandle>.Fail(
                "Attachment not found.", 404, TaskReasonCodes.AttachmentNotFound, request.CorrelationId);
        }

        // ObjectKey is never touched here — OpenReadAsync resolves it internally from ContentId (AD-5).
        var opened = await documentRepository.OpenReadAsync(attachment.ContentId, request.CorrelationId, ct);
        if (!opened.IsSuccessful || opened.Data is null)
        {
            return Response<TaskAttachmentContentHandle>.Fail(
                opened.Errors, opened.StatusCode == 0 ? 503 : opened.StatusCode, opened.ReasonCode, request.CorrelationId);
        }

        return Response<TaskAttachmentContentHandle>.Success(
            new TaskAttachmentContentHandle(
                opened.Data.Content, opened.Data.MediaType, opened.Data.FileName, opened.Data.ByteSize),
            correlationId: request.CorrelationId);
    }
}
