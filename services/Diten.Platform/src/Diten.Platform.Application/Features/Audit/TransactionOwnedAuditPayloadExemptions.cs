namespace Diten.Platform.Application.Features.Audit;

/// <summary>
/// WP-LANE-MAIN-SYNC-02 P1 (owner decision 2026-10-14, BL-634) — THE ONLY events the in-transaction audit door accepts
/// with a hand-written payload instead of an intent. They came with main-sync 2026-10-14: MOD-0022 e-signature writes its
/// MOD-0021 record itself (<c>ESignatureAuditIntent.EnqueueAsync</c>, full <c>audit_events</c> shape) and its code is not
/// ours to change (XMC-001).
///
/// <para>⚠ EXACT event names — no prefix, no wildcard. An event that is not named here and carries no intent is still
/// refused (<see cref="TransactionOwnedAuditIntentInvalidException"/>). A named event's payload must name the same event.
/// The list only shrinks: a row leaves when its handler moves to an intent. Expires 2026-11-13 (BL-634).</para>
/// </summary>
public static class TransactionOwnedAuditPayloadExemptions
{
    public const string Owner = "MOD-0022 e-imza geliştiricisi";
    public const string Reason = "main-sync 2026-10-14";
    public const string Backlog = "BL-634";
    public const string ExpiresOn = "2026-11-13";

    private static readonly HashSet<string> Events = new(StringComparer.Ordinal)
    {
        "esign.signature.signed",
        "esign.signature.invalidated",
        "esign.request.created",
        "esign.request.rejected",
        "esign.request.cancelled",
        "esign.request.expired",
        "esign.policy.changed",
        "esign.policy.override-reverted",
        "esign.meaning.created",
        "esign.meaning.updated",
        "esign.meaning.activated",
        "esign.meaning.deactivated",
        "esign.export.created",
        "esign.sample.submitted",
        "esign.sample.approved",
        "esign.sample.rejected",
        "esign.sample.deactivated",
        "esign.sample.withdrawn"
    };

    public static IReadOnlyCollection<string> All => Events;

    public static bool Exempts(string? eventName) => eventName is not null && Events.Contains(eventName);
}
