using Diten.Platform.Common.Persistence;

namespace Diten.Platform.Domain.Entities.DocumentManagement;

/// <summary>
/// MOD-0029-FU01 (post-approval, user-directed extension) — per-user favorite ("star") marker for a controlled
/// document. Tenant + user + document scoped; toggling adds/soft-removes this sidecar row. Never mutates the
/// document itself.
/// </summary>
public sealed class DocumentFavorite : TenantScopedEntity
{
    public required Guid UserId { get; set; }
    public required Guid DocumentId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
