using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Application.Features.Tasks.Attachments;

/// <summary>Client-facing attachment metadata. No storage detail (object key) is ever projected — the pointer is
/// <see cref="Id"/> plus <c>ContentId</c> internally; the client addresses content only by attachment id.</summary>
public sealed record TaskAttachmentDto(
    Guid Id,
    Guid TaskItemId,
    string? ChecklistItemCode,
    TaskAttachmentKind Kind,
    string FileName,
    string MediaType,
    long ByteSize,
    string? Note,
    Guid UploadedByUserId,
    DateTimeOffset UploadedAt);

/// <summary>Resolved handle for streaming a download. Never serialized — the controller consumes it directly.</summary>
public sealed record TaskAttachmentContentHandle(Stream Content, string MediaType, string FileName, long ByteSize);
