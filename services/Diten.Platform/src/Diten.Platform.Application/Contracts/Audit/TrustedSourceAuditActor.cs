namespace Diten.Platform.Application.Contracts.Audit;

/// <summary>
/// WP-GP-AUDIT-TRAIL-01 D1 — WHO acted, as the central audit row records it, for an intent a trusted source service
/// (MDM) sent. Resolved by Platform at acceptance: the source names its actor by id only, and no personal data travels
/// in the source envelope or rests in the source's documents.
/// </summary>
/// <param name="UserId">The acting user, when the source's actor id is a user id. Null for a non-person actor — the row
/// then stays a SERVICE row, exactly as before.</param>
/// <param name="DisplayNameMasked">That user's name, masked by Platform's own audit rule; null when it could not be
/// resolved (the row is still written).</param>
public sealed record TrustedSourceAuditActor(Guid? UserId, string? DisplayNameMasked)
{
    public static TrustedSourceAuditActor None { get; } = new(null, null);
}
