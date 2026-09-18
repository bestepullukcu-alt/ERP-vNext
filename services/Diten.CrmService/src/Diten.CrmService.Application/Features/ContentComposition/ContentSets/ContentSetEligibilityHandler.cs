using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.ContentComposition.Eligibility;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.ContentSets;

/// <summary>
/// SCMM-14 (D14-c) apply-eligibility. Builds one <see cref="EligibilityContext"/> from the set's pinned scope + the
/// languages of its selected components, then evaluates the eligibility policy referenced by EACH selected claim
/// (<c>Claim.Applicability.EligibilityPolicyId</c> — the only policy anchor in the model) through the in-process
/// <see cref="IEligibilityEvaluationPort"/>, and writes a per-claim snapshot. NON-BLOCKING: a Blocked / Unresolved
/// outcome never fails the draft (a hard gate is SCMM-15/17). FAIL-CLOSED: an infrastructure failure of the port
/// PROPAGATES as a thrown exception (never folded into an Unresolved snapshot item). A selected claim that carries no
/// eligibility policy is recorded as Unresolved (nothing proves it eligible) — never silently omitted.
/// </summary>
public sealed class ApplyContentSetEligibilityHandler : ContentSetWriteHandlerBase,
    IRequestHandler<ApplyContentSetEligibilityCommand, Response<bool>>
{
    private readonly IContentScopeRepository _scopes;
    private readonly IClaimRepository _claims;
    private readonly IEligibilityEvaluationPort _port;

    public ApplyContentSetEligibilityHandler(
        ITenantContext tenant, IActorContext actor, IContentSetRepository sets,
        IContentScopeRepository scopes, IClaimRepository claims, IEligibilityEvaluationPort port,
        IContentCompositionAuditPublisher? audit = null)
        : base(tenant, actor, sets, audit)
    {
        _scopes = scopes;
        _claims = claims;
        _port = port;
    }

    public async Task<Response<bool>> Handle(ApplyContentSetEligibilityCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (set, tenantId, error, status) = await LoadEditableAsync(request.ContentSetId, cancellationToken);
        if (set is null)
        {
            return Response<bool>.Fail(error!, status);
        }

        var context = await BuildContextAsync(tenantId, set, cancellationToken);
        var pinned = BuildPinnedSelections(set);
        var now = DateTimeOffset.UtcNow;
        var items = new List<ContentSetEligibilityItem>();

        foreach (var selectedClaim in set.SelectedClaims)
        {
            var claim = await _claims.GetByIdAsync(tenantId, selectedClaim.ClaimId, cancellationToken);
            var policyId = claim?.Applicability.EligibilityPolicyId;

            if (claim is null)
            {
                items.Add(Unresolved(selectedClaim, null, "claim-missing"));
                continue;
            }

            if (policyId is not { } pid || pid == Guid.Empty)
            {
                items.Add(Unresolved(selectedClaim, null, "no-eligibility-policy"));
                continue;
            }

            // FAIL-CLOSED: EvaluateAsync is NOT wrapped — an infrastructure failure throws and propagates.
            var response = await _port.EvaluateAsync(
                new ResolveEligibilityQuery(pid, context, pinned, now), cancellationToken);

            if (response.IsSuccessful && response.Data is { } result)
            {
                items.Add(new ContentSetEligibilityItem
                {
                    ItemKind = "claim",
                    SelectionId = selectedClaim.SelectionId,
                    ItemId = selectedClaim.ClaimId,
                    PolicyId = pid,
                    State = result.State.ToString().ToLowerInvariant(),
                    BlockingLevel = result.BlockingLevel,
                    Reason = result.Reason,
                    PolicyVersion = result.PolicyVersion
                });
            }
            else
            {
                // A non-success Response (e.g. policy not found) is a decision, not an infra failure: record Unresolved.
                items.Add(Unresolved(selectedClaim, pid,
                    response.Errors is { Count: > 0 } ? response.Errors[0] : "eligibility-unresolved"));
            }
        }

        set.EligibilitySnapshot = new ContentSetEligibilitySnapshot { EvaluatedAtUtc = now, Items = items };
        Stamp(set);
        await Sets.UpdateAsync(set, cancellationToken);
        await PublishAsync(ContentSetReasonCodes.EligibilityApplied, tenantId, set, cancellationToken);
        return Response<bool>.Success(true);
    }

    private static ContentSetEligibilityItem Unresolved(ContentSetClaim c, Guid? policyId, string reason) => new()
    {
        ItemKind = "claim",
        SelectionId = c.SelectionId,
        ItemId = c.ClaimId,
        PolicyId = policyId,
        State = EligibilityState.Unresolved.ToString().ToLowerInvariant(),
        BlockingLevel = "policy",
        Reason = reason
    };

    private async Task<EligibilityContext> BuildContextAsync(Guid tenantId, ContentSet set, CancellationToken ct)
    {
        var dimensions = new List<EligibilityContextDimension>();
        var languages = new List<string>();

        if (set.Scope is { } scopeRef)
        {
            var scope = await _scopes.GetByIdAsync(tenantId, scopeRef.ContentScopeId, ct);
            if (scope is not null)
            {
                AddDimension(dimensions, "product", scope.ProductRefs);
                AddDimension(dimensions, "market", scope.MarketRefs);
                AddDimension(dimensions, "audience", scope.AudienceRefs);
                if (!string.IsNullOrWhiteSpace(scope.Channel)) { AddDimension(dimensions, "channel", new[] { scope.Channel }); }
                if (!string.IsNullOrWhiteSpace(scope.LanguageCode)) { languages.Add(scope.LanguageCode); }
            }
        }

        // The selected components' languages fold into the language dimension (scope + components).
        languages.AddRange(set.SelectedComponents.Select(c => c.LanguageCode).Where(l => !string.IsNullOrWhiteSpace(l)));
        AddDimension(dimensions, "language", languages);

        return new EligibilityContext(dimensions);
    }

    private static void AddDimension(List<EligibilityContextDimension> dims, string key, IEnumerable<string> values)
    {
        var clean = values.Select(v => v?.Trim() ?? string.Empty).Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (clean.Count > 0)
        {
            dims.Add(new EligibilityContextDimension(key, clean));
        }
    }

    private static IReadOnlyList<string> BuildPinnedSelections(ContentSet set)
        => set.SelectedComponents.Select(c => $"content:{c.KnowledgeContentId}@{c.ContentVersion}")
            .Concat(set.SelectedClaims.Select(c => $"claim:{c.ClaimId}@{c.ClaimVersion}"))
            .ToList();
}
