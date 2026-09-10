using Diten.CrmService.Application.Common.Models;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>
/// SCMM-11 (CAND-CAP-0011) — the in-process gate a consumer (e.g. SCMM-14 content assembly) calls to check eligibility.
/// A THIN adapter over the single <see cref="ResolveEligibilityQuery"/> resolver (MOD-0029 port pattern): it makes no
/// decision of its own and adds no RBAC — it returns the same <c>Response&lt;EligibilityResult&gt;</c> the query returns,
/// and the CALLER applies the gate rule (allow ONLY when State == Eligible; Blocked / Unresolved / a thrown exception ⇒
/// refuse). FAIL-CLOSED: an infrastructure failure of the underlying read propagates as a thrown exception, never as an
/// <c>Unresolved</c> result.
/// </summary>
public interface IEligibilityEvaluationPort
{
    Task<Response<EligibilityResult>> EvaluateAsync(ResolveEligibilityQuery query, CancellationToken cancellationToken);
}

/// <summary>
/// SCMM-11 (RM4) — writes the evaluation log (input context + pinned selections + result + reasons + policy version).
/// Recording is a side effect: an implementation is fail-soft (a log outage never changes or blocks the decision), and
/// it never fabricates or alters a result.
/// </summary>
public interface IEligibilityEvaluationLogWriter
{
    Task WriteAsync(EligibilityEvaluationLogEntry entry, CancellationToken cancellationToken);
}
