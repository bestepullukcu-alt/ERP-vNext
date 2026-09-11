using Diten.MdmService.Domain.Entities;

namespace Diten.MdmService.Domain.Repositories;

public enum FinishedGoodCreationAttemptOutcome
{
    Bound,
    Conflict,
    Unavailable
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
}
