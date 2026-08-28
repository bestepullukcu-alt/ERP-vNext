using Diten.Platform.Application.Features.WorkAggregation.Providers;

namespace Diten.Platform.Application.Features.WorkAggregation.Dispatch;

// WC-D2 (DCP-004 §2 D2) — THE ACTION AS IT LOOKS ON THE WIRE.
//
// WHAT WAS MEASURED. The projection has always emitted a single authoritative actions[] — code, label, enabled,
// disabledReasonCode, requiresReason, riskLevel — and NONE of it named an endpoint, a method or a permission
// key. So the browser had to know, per provider, where a button's write goes. It knew for exactly one:
//
//     const isRealTaskItem = (item) =>
//         item && item.provenance !== 'fixture' && item.source?.providerCode === 'tasks';
//
// Everything else fell to a browser-side "transition" that changed the screen and nothing else, and said so in
// the console: "no backend owns it". A second provider (MOD-0023 approvals) has been on the board since WC-1
// with four real endpoints behind it, and not one of its buttons ever reached them.
//
// THE DECISION (owner, 2026-08-28). The browser writes to ONE address —
//     POST /api/v1/work-items/{itemId}/actions/{actionCode}
// — and Platform resolves where it goes. No module's address is ever known to the browser, so a provider added
// tomorrow inherits a working button instead of a dead one, and the hardcoded branch above dies of its own
// accord: every provider now gets the same URL.
public static class WorkItemActionReasonCodes
{
    /// <summary>No provider by that code is bound at all.</summary>
    public const string ProviderUnknown = "WORK_ITEM_PROVIDER_UNKNOWN";

    /// <summary>
    /// A bound provider that publishes actions but has no dispatcher behind it.
    ///
    /// <para>ITS OWN CODE, deliberately separate from <see cref="ProviderUnknown"/>: "there is no such source"
    /// and "the source exists and cannot be written to" send a reader to two different people. Silence is what
    /// this whole slice removes, so the one thing this must never be is a 200.</para>
    /// </summary>
    public const string ProviderNotDispatchable = "WORK_ITEM_PROVIDER_NOT_DISPATCHABLE";

    /// <summary>An action code this provider does not publish (a stale screen, or a typo on the wire).</summary>
    public const string ActionUnknown = "WORK_ITEM_ACTION_UNKNOWN";

    /// <summary>The caller does not hold the permission this action's endpoint requires. Decided SERVER-side.</summary>
    public const string ActionForbidden = "WORK_ITEM_ACTION_FORBIDDEN";

    /// <summary>The action needs a field the request did not carry (a reason, a date, a person).</summary>
    public const string PayloadInvalid = "WORK_ITEM_ACTION_PAYLOAD_INVALID";

    /// <summary>
    /// WC-D1 — the module owning this item lives in ANOTHER SERVICE and did not answer: refused the connection,
    /// exceeded its budget, or replied with something that is not a projection envelope.
    ///
    /// <para><b>ITS OWN CODE, and the whole point of the code is that it is not a 200.</b> Every other refusal
    /// above is a permanent fact about the wiring — no such source, no dispatcher, no such verb, no permission.
    /// This one is TRANSIENT and sends the reader somewhere else entirely: try again, or tell an operator that a
    /// service is down. Folding it into <see cref="ProviderNotDispatchable"/> would tell a user whose network
    /// blipped that an administrator must change a configuration.</para>
    ///
    /// <para><b>FAIL-CLOSED.</b> A write whose answer never arrived is reported as REFUSED, never as success. The
    /// action MAY have been carried out on the far side — that is exactly why the caller must be told the outcome
    /// is unknown rather than shown a green toast over a write nobody can confirm.</para>
    /// </summary>
    public const string RemoteUnavailable = "WORK_ITEM_REMOTE_UNAVAILABLE";
}

/// <summary>
/// Everything an action may need to carry, declared ONCE.
///
/// <para>Deliberately a union rather than a per-action body: the browser posts to one address and must not have
/// to know which DTO the module behind it expects — that knowledge is precisely what this slice moves to the
/// server. Each dispatcher picks the fields its own command needs and REFUSES, with
/// <see cref="WorkItemActionReasonCodes.PayloadInvalid"/>, when a required one is missing. A field silently
/// dropped would be the "400 The Reason field is required" defect (BL-043) wearing a server-side hat.</para>
///
/// <para>Every member is optional because optionality is per ACTION, not per field: <c>reason</c> is required
/// for <c>inquire</c> and meaningless for <c>claim</c>.</para>
/// </summary>
public sealed record WorkItemActionPayloadDto(
    int? ExpectedVersion = null,
    string? Reason = null,
    string? ReasonCode = null,
    string? Note = null,
    DateTimeOffset? PlannedDate = null,
    Guid? AssigneeUserId = null,
    Guid? WaitingOnUserId = null,
    string? Comment = null,
    string? EvidenceRef = null,
    /// <summary>MOD-0023 only: who a delegation goes to, or whom information is requested from.</summary>
    string? TargetPrincipalId = null,
    /// <summary>
    /// MOD-0023 only. Supplied by the caller when it has one so a retried click is not a second decision;
    /// the dispatcher mints one when it does not, because the endpoint requires the field and a request that
    /// cannot be made is worse than one that is merely not idempotent.
    /// </summary>
    string? IdempotencyKey = null);

/// <summary>The wire body of the single write endpoint.</summary>
/// <param name="ProviderCode">
/// WHICH source owns the item, copied from the projection's own <c>source.providerCode</c>.
///
/// <para>This is ADDRESSING, never authority. Item ids are per-module identifiers and nothing in them says which
/// table they came from, so naming the provider is how a lookup is even possible. Naming the wrong one costs the
/// caller a 404 from a module that has never heard of the id — it cannot widen what they may do, because the
/// permission is evaluated from CLAIMS on the server and the module re-checks its own rules underneath.</para>
/// </param>
public sealed record WorkItemActionRequestDto(
    string ProviderCode,
    WorkItemActionPayloadDto? Payload = null);

/// <summary>What the caller is told when the write went through. Deliberately thin: the projection is re-read
/// afterwards and IS the new state, so echoing a status here would invite the browser to trust it.</summary>
public sealed record WorkItemActionResultDto(
    string ItemId,
    string ProviderCode,
    string ActionCode);

/// <summary>One dispatch, with the actor resolved SERVER-side.</summary>
public sealed record WorkItemActionDispatchRequest(
    Guid ItemId,
    string ActionCode,
    WorkItemActionPayloadDto Payload,
    WorkItemActor Actor,
    string CorrelationId);
