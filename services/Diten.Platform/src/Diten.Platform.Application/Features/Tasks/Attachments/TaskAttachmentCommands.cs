using Diten.Platform.Application.Common;
using Diten.Platform.Application.Contracts.Audit;
using Diten.Platform.Domain.Enums;
using Diten.Platform.Domain.Enums.Tasks;
using MediatR;

namespace Diten.Platform.Application.Features.Tasks.Attachments;

// MOD-0024 Slice ATT-1 — task attachments. IAuditableCommand on add/remove per AC5; list/open are reads.

internal static class TaskAttachmentAudit
{
    public const string Module = "MOD-0024-ATT1";
    public static Guid? Correlation(string? correlationId) => Guid.TryParse(correlationId, out var c) ? c : null;
}

/// <summary>
/// <see cref="Content"/> is a forward-only stream from the multipart request (AD-4 — never base64, never
/// buffered whole in a JSON body). The caller owns the stream's lifetime.
/// </summary>
public sealed record AddTaskAttachmentCommand(
    Guid TaskItemId,
    Stream Content,
    string FileName,
    string? MediaType,
    TaskAttachmentKind Kind,
    string? ChecklistItemCode,
    string? Note,
    string CorrelationId) : IRequest<Response<TaskAttachmentDto>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.Tasks, AuditOperation.Create, "TaskAttachment",
        EntityId: TaskItemId, SourceModule: TaskAttachmentAudit.Module,
        CorrelationId: TaskAttachmentAudit.Correlation(CorrelationId),
        Metadata: new Dictionary<string, object?> { ["fileName"] = FileName, ["kind"] = Kind.ToString() });
}

/// <summary>Soft delete only. The stored object is never touched (AD-6 — no purge path here).</summary>
public sealed record RemoveTaskAttachmentCommand(Guid TaskItemId, Guid AttachmentId, string CorrelationId)
    : IRequest<Response<NoContent>>, IAuditableCommand, IAuditMetadataProvider
{
    public AuditRequestMetadata GetAuditMetadata() => new(
        AuditCategory.Tasks, AuditOperation.Delete, "TaskAttachment",
        EntityId: AttachmentId, SourceModule: TaskAttachmentAudit.Module,
        CorrelationId: TaskAttachmentAudit.Correlation(CorrelationId));
}

public sealed record ListTaskAttachmentsQuery(Guid TaskItemId, string CorrelationId)
    : IRequest<Response<IReadOnlyList<TaskAttachmentDto>>>;

/// <summary>Resolves an attachment id to a readable stream. The object key is never taken from the caller —
/// it is looked up from the tenant-scoped repository's own pointer (MOD-0262-FU01 AD-5).</summary>
public sealed record OpenTaskAttachmentQuery(Guid TaskItemId, Guid AttachmentId, string CorrelationId)
    : IRequest<Response<TaskAttachmentContentHandle>>;
