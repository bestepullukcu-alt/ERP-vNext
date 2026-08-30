using Diten.Shared.Core;

namespace Diten.PpmService.Application.Common;


public static class PpmAccessDecisionExtensions
{
    public static Response<T> Failure<T>(this PpmAccessDecision decision) =>
        decision == PpmAccessDecision.DependencyUnavailable
            ? Response<T>.Fail("Authoritative entitlement decision is unavailable.", 503)
            : Response<T>.Fail("Forbidden.", 403);
}
