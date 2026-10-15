using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

// FG FIX1 item 7: one refusal class per cause — a request the store cannot accept (Invalid, 400), the store not
// answering (Unavailable, 503 — the same key may be sent again), and a command an older build left half-done
// (ReconciliationRequired, 202). Formerly all three were "Unavailable" and answered 202.
public enum FinishedGoodCreationAttemptOutcome
{
    Bound,
    Conflict,
    Unavailable,
    Invalid,
    ReconciliationRequired
}

public sealed record FinishedGoodCreationAttemptResult(
    FinishedGoodCreationAttemptOutcome Outcome,
    FinishedGoodCreationAttempt? Attempt)
{
    public static FinishedGoodCreationAttemptResult Bound(FinishedGoodCreationAttempt attempt)
        => new(FinishedGoodCreationAttemptOutcome.Bound, attempt);

    public static FinishedGoodCreationAttemptResult Conflict(FinishedGoodCreationAttempt? attempt = null)
        => new(FinishedGoodCreationAttemptOutcome.Conflict, attempt);

    public static FinishedGoodCreationAttemptResult Unavailable()
        => new(FinishedGoodCreationAttemptOutcome.Unavailable, null);

    public static FinishedGoodCreationAttemptResult Invalid()
        => new(FinishedGoodCreationAttemptOutcome.Invalid, null);

    public static FinishedGoodCreationAttemptResult ReconciliationRequired()
        => new(FinishedGoodCreationAttemptOutcome.ReconciliationRequired, null);
}
