using Diten.Platform.Common.Persistence;
using Diten.Platform.Domain.Enums.DocumentManagement;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU14 — a relation between an external document and an internal FU06
/// <see cref="DocumentMasterRegisterEntry"/> (GMG-QMS-SOP-0001 §10.3). It answers "which of our SOPs implement or
/// depend on this external requirement" so that a source change can be traced to the internal documents it touches.
///
/// BOUNDARY: the link is traceability only. It never changes the internal document's lifecycle status; the most it
/// can do is carry <see cref="ExternalDocumentLinkStatus.ActionRequired"/> for a human to act on. Links are
/// tenant-scoped on both sides (cross-tenant linking is refused) and are never hard-deleted — closing is a status
/// change.
/// </summary>
public sealed class ExternalDocumentInternalLink : TenantScopedEntity
{
    public required Guid ExternalDocumentRegisterEntryId { get; set; }
    public required Guid InternalRegisterEntryId { get; set; }

    public ExternalDocumentLinkType LinkType { get; set; } = ExternalDocumentLinkType.References;
    public ExternalDocumentLinkStatus LinkStatus { get; set; } = ExternalDocumentLinkStatus.Active;

    public string? Notes { get; set; }

    public string? CorrelationId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
