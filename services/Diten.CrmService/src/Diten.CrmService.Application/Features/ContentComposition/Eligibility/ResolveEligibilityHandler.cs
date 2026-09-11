using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Eligibility;

/// <summary>
/// SCMM-11 (CAND-CAP-0011, docx C2) — the ONE deterministic eligibility resolver. It reads the policy (by id) and maps
/// the supplied <see cref="EligibilityContext"/> to a disjoint <see cref="EligibilityState"/>, then records the RM4
/// evaluation log. Mirrors the MOD-0029 effectiveness resolver:
/// <list type="bullet">
/// <item>FAIL-CLOSED: the policy read is NOT wrapped in try/catch — an infrastructure failure ("could not check")
/// propagates as a thrown exception, never folded into an <c>Unresolved</c> result.</item>
/// <item>A required context dimension that is missing ⇒ <c>Unresolved</c> (the gate cannot decide) — never a silent
/// default, never a permission.</item>
/// <item>Only a published, effective, non-archived policy yields Eligible/Blocked; otherwise <c>Unresolved</c>
/// (policy-not-effective) — the gate never trusts a draft/expired policy.</item>
/// </list>
/// It computes NO segment membership (MOD-0167) and advances no content (execution) — it evaluates context ↔ policy only.
/// Deterministic: the same (policy, context, instant) always yields the same result.
/// </summary>
public sealed class ResolveEligibilityHandler : IRequestHandler<ResolveEligibilityQuery, Response<EligibilityResult>>
{
    private readonly ITenantContext _tenant;
    private readonly IActorContext _actor;
    private readonly IEligibilityPolicyRepository _policies;
    private readonly IEligibilityEvaluationLogWriter _log;

    public ResolveEligibilityHandler(
        ITenantContext tenant,
        IActorContext actor,
        IEligibilityPolicyRepository policies,
        IEligibilityEvaluationLogWriter log)
    {
        _tenant = tenant;
        _actor = actor;
        _policies = policies;
        _log = log;
    }

    public async Task<Response<EligibilityResult>> Handle(ResolveEligibilityQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<EligibilityResult>.Fail("Tenant context is required.", 400);
        }

        if (request.PolicyId == Guid.Empty)
        {
            return Response<EligibilityResult>.Fail("PolicyId is required.", 400);
        }

        var at = request.At ?? DateTimeOffset.UtcNow;

        // FAIL-CLOSED: not guarded — an infrastructure failure throws through (never a silent Unresolved).
        var policy = await _policies.GetByIdAsync(tenantId, request.PolicyId, cancellationToken);
        if (policy is null)
        {
            return Response<EligibilityResult>.Fail("Eligibility policy not found.", 404);
        }

        var context = request.Context ?? new EligibilityContext(Array.Empty<EligibilityContextDimension>());

        // Only a published + effective + non-archived policy is trusted for an Eligible/Blocked decision.
        var result = policy.IsArchived() || !policy.IsPublished() || !policy.IsEffectiveAt(at)
            ? new EligibilityResult(
                EligibilityState.Unresolved, "policy", EligibilityReasonCodes.PolicyNotEffective,
                policy.Id, policy.PolicyVersion, Array.Empty<EligibilityConditionOutcome>(), at)
            : Evaluate(policy, context, at);

        await WriteLogFailSoftAsync(tenantId, policy, context, request.PinnedSelections, result, cancellationToken);

        return Response<EligibilityResult>.Success(result);
    }

    /// <summary>Deterministic condition evaluation. Overall precedence: any missing-required ⇒ Unresolved; else any
    /// violated ⇒ Blocked; else Eligible. A non-required condition whose dimension is absent from the context is treated
    /// as not-applicable (pass) — only a REQUIRED missing dimension is Unresolved.</summary>
    private static EligibilityResult Evaluate(EligibilityPolicy policy, EligibilityContext context, DateTimeOffset at)
    {
        var ctx = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var dimension in context.Dimensions)
        {
            if (string.IsNullOrWhiteSpace(dimension.Dimension))
            {
                continue;
            }

            var key = dimension.Dimension.Trim();
            if (!ctx.TryGetValue(key, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                ctx[key] = set;
            }

            foreach (var value in dimension.Values ?? Enumerable.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    set.Add(value.Trim());
                }
            }
        }

        var outcomes = new List<EligibilityConditionOutcome>(policy.Conditions.Count);
        foreach (var condition in policy.Conditions)
        {
            var match = EligibilityMatchKinds.Normalize(condition.Match);
            var values = (condition.Values ?? new List<string>())
                .Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v.Trim()).ToList();
            var dimensionKey = condition.Dimension?.Trim() ?? string.Empty;
            var present = ctx.TryGetValue(dimensionKey, out var have) && have.Count > 0;

            EligibilityState state;
            string? reason;
            if (!present)
            {
                if (condition.Required)
                {
                    state = EligibilityState.Unresolved;
                    reason = $"{EligibilityReasonCodes.MissingRequiredContext}:{dimensionKey}";
                }
                else
                {
                    state = EligibilityState.Eligible; // absent + not required = not applicable
                    reason = null;
                }
            }
            else
            {
                var intersects = have!.Overlaps(values);
                if (match == EligibilityMatchKinds.Excludes)
                {
                    state = intersects ? EligibilityState.Blocked : EligibilityState.Eligible;
                    reason = intersects ? $"{EligibilityReasonCodes.Excluded}:{dimensionKey}" : null;
                }
                else
                {
                    state = intersects ? EligibilityState.Eligible : EligibilityState.Blocked;
                    reason = intersects ? null : $"{EligibilityReasonCodes.NotIncluded}:{dimensionKey}";
                }
            }

            outcomes.Add(new EligibilityConditionOutcome(condition.Dimension ?? string.Empty, match, state, reason));
        }

        var unresolved = outcomes.FirstOrDefault(o => o.State == EligibilityState.Unresolved);
        if (unresolved is not null)
        {
            return new EligibilityResult(
                EligibilityState.Unresolved, "context", unresolved.Reason, policy.Id, policy.PolicyVersion, outcomes, at);
        }

        var blocked = outcomes.FirstOrDefault(o => o.State == EligibilityState.Blocked);
        if (blocked is not null)
        {
            return new EligibilityResult(
                EligibilityState.Blocked, "policy", blocked.Reason, policy.Id, policy.PolicyVersion, outcomes, at);
        }

        return new EligibilityResult(EligibilityState.Eligible, null, null, policy.Id, policy.PolicyVersion, outcomes, at);
    }

    private async Task WriteLogFailSoftAsync(
        Guid tenantId, EligibilityPolicy policy, EligibilityContext context,
        IReadOnlyList<string>? pinnedSelections, EligibilityResult result, CancellationToken cancellationToken)
    {
        try
        {
            var reasons = result.Conditions.Where(o => o.Reason is not null).Select(o => o.Reason!).ToList();
            if (reasons.Count == 0 && result.Reason is not null)
            {
                reasons.Add(result.Reason);
            }

            await _log.WriteAsync(
                new EligibilityEvaluationLogEntry(
                    tenantId, policy.Id, policy.PolicyVersion, context,
                    pinnedSelections ?? Array.Empty<string>(), result.State, result.BlockingLevel, reasons,
                    result.EvaluatedAtUtc, _actor.ActorName),
                cancellationToken);
        }
        catch
        {
            // Fail-soft: the decision is already computed; an evaluation-log outage never changes or blocks it.
        }
    }
}
