using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.Tasks;

namespace Diten.Platform.Domain.Entities.Tasks;

/// <summary>
/// MOD-0024 Slice ATT-1 — one file attached to a task, stored through the Internal Document Repository Service
/// (MOD-0262-FU01) and referenced here by <see cref="ContentId"/> only.
///
/// <para><b>Why no embedded <c>ContentRef</c> / <c>ObjectKey</c>.</b> The pack's own reference implementation
/// (MOD-0029's <c>DocumentVersioningService</c>) calls the low-level <c>IContentStorageGateway</c> directly and
/// keeps its own <c>ContentRef</c> copy, including the object key, because it pre-dates MOD-0262-FU01 and owns
/// its own source of truth. This slice instead calls the HIGH-level <c>DocumentRepositoryService</c> — the one
/// with <c>CompensateAsync</c>, which this slice needs — and that service's own client-facing model
/// (<c>RepositoryObjectModel</c>) deliberately never exposes <c>ObjectKey</c> (MOD-0262-FU01 AD-4/AD-5: the key
/// is an internal storage detail and never leaves the repository's boundary). Storing only <see cref="ContentId"/>
/// here — plus the display fields the model DOES expose — keeps that guarantee intact for a second collection
/// instead of re-creating the leak the repository was built to close. Reading the file back
/// (<c>OpenTaskAttachmentQuery</c>) re-resolves the key from <c>ContentId</c> through the repository every time.</para>
///
/// <para><b>No field on <c>TaskItem</c>.</b> Per the pack: attachments are a collection queried by
/// <see cref="TaskId"/>, not an embedded list — a task with many large attachments must not make every task read
/// heavier.</para>
/// </summary>
public sealed class TaskAttachment : TenantScopedEntity
{
    public required Guid TaskId { get; set; }

    /// <summary>Set only when this attachment was uploaded as evidence FOR a specific checklist item — the join
    /// key <c>SetChecklistItemStateHandler</c>'s evidence gate counts against. Null for a general attachment.</summary>
    public string? ChecklistRunItemCode { get; set; }

    public required TaskAttachmentKind Kind { get; set; }

    /// <summary>The repository's own pointer — the ONLY thing needed to open or compensate this file later.</summary>
    public required Guid ContentId { get; set; }

    /// <summary>Frozen display fields, copied from the repository's store result at upload time (not re-read on
    /// every list — same reasoning DCP-005 applies to a frozen citation: what the list shows is what was true
    /// when the file was uploaded).</summary>
    public required string FileName { get; set; }
    public required string MediaType { get; set; }
    public long ByteSize { get; set; }
    public string? Checksum { get; set; }

    /// <summary>The uploader's own note, ≤500 chars (enforced at the command boundary).</summary>
    public string? Note { get; set; }

    public required Guid UploadedByUserId { get; set; }
    public required DateTimeOffset UploadedAt { get; set; }

    /// <summary>
    /// Soft delete only (multi-tenancy rule: <c>IsDeleted</c> + <c>DeletedAt</c> together). The stored object is
    /// NEVER removed on a task-attachment delete — AD-6 has no purge path, and this is a record of what was once
    /// attached, not a file-manager delete. <c>BaseEntity.IsDeleted</c> is the query filter; this is the audit
    /// timestamp.
    /// </summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
