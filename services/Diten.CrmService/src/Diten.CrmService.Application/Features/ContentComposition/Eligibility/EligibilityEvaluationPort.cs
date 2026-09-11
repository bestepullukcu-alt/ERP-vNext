using Diten.CrmService.Application.Common.Models;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>
/// SCMM-11 (CAND-CAP-0011) — the in-process gate implementation: a thin wrapper over <see cref="IMediator"/> that routes
/// to the single <see cref="ResolveEligibilityQuery"/> resolver (MOD-0029 port pattern). It makes no decision and adds no
/// RBAC — the port result IS the query result (so a consumer and a direct <c>Send</c> can never diverge). FAIL-CLOSED:
/// the resolver's policy read is not caught anywhere below it, so an infrastructure failure throws straight through.
/// </summary>
public sealed class EligibilityEvaluationPort : IEligibilityEvaluationPort
{
    private readonly IMediator _mediator;

    public EligibilityEvaluationPort(IMediator mediator) => _mediator = mediator;

    public Task<Response<EligibilityResult>> EvaluateAsync(ResolveEligibilityQuery query, CancellationToken cancellationToken)
        => _mediator.Send(query, cancellationToken);
}
