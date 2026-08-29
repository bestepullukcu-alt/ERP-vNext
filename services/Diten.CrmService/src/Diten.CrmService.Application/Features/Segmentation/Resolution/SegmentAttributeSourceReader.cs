using Diten.CrmService.Application.Features.ConsentPreference.Evaluation;
using Diten.CrmService.Application.Features.Segmentation.Catalog;
using Diten.CrmService.Domain.Entities;

namespace Diten.CrmService.Application.Features.Segmentation.Resolution;

/// <summary>
/// MOD-0167 FU02 Phase-1.5 + Phase-2 attribute loader. It walks the criteria tree once, decides which sources the rule
/// actually needs, reads each of them <b>exactly once in bulk</b> for the whole candidate set, and hands the evaluator
/// a fully-resolved value set per candidate.
/// <para>Cost therefore grows with the number of SOURCES a rule touches, never with the number of candidates: the
/// consent rows, the coverage rows, the links and the concept-affinity specialty set are each fetched or derived once
/// and applied to everyone. A per-candidate read would be the N+1 the scale contract forbids, and a call-counter test
/// pins it down.</para>
/// <para>Fail-closed per D6: an in-service source that cannot answer marks the candidate UNRESOLVED with its specific
/// reason code (consent_unknown, territory_coverage_unavailable, concept_product_node_missing, ...) and the resolution
/// still completes. Nothing here calls a cross-process dependency and nothing here writes.</para>
/// </summary>
public sealed class SegmentAttributeSourceReader : ISegmentAttributeSourceReader
{
    private readonly ISegmentCandidateSource _candidates;
    private readonly ISegmentConsentBulkReader _consent;
    private readonly ISegmentTerritoryCoverageReader _territory;
    private readonly ISegmentConceptAffinityReader _affinity;

    public SegmentAttributeSourceReader(
        ISegmentCandidateSource candidates,
        ISegmentConsentBulkReader consent,
        ISegmentTerritoryCoverageReader territory,
        ISegmentConceptAffinityReader affinity)
    {
        _candidates = candidates;
        _consent = consent;
        _territory = territory;
        _affinity = affinity;
    }

    public async Task<SegmentAttributeContext> LoadAsync(
        Guid tenantId,
        Segment segment,
        IReadOnlyList<SegmentSubjectSnapshot> candidates,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        var predicates = segment.Criteria.Where(n => n.IsPredicate()).ToList();
        var sets = candidates.ToDictionary(
            c => c.SubjectId,
            c => new SegmentAttributeValueSet(c.SubjectId, c.SubjectType));

        if (predicates.Count == 0 || candidates.Count == 0)
        {
            return new SegmentAttributeContext(sets);
        }

        var isContactSegment = string.Equals(
            segment.SubjectType, SegmentSubjectTypes.Contact, StringComparison.Ordinal);
        var subjectIds = candidates.Select(c => c.SubjectId).ToList();
        var usedCodes = predicates
            .Select(p => (p.AttributeCode ?? string.Empty).Trim().ToLowerInvariant())
            .ToHashSet(StringComparer.Ordinal);

        // ---- Phase 1: native values straight off the projection (no extra read at all) ----
        foreach (var candidate in candidates)
        {
            ApplyNative(predicates, candidate, sets[candidate.SubjectId]);
        }

        // ---- Phase 1.5: ONE bulk link read, only when a join or a contact-side territory rule needs it ----
        var needsLinks = usedCodes.Overlaps(new[]
        {
            SegmentAttributeCatalog.ContactAccountRole,
            SegmentAttributeCatalog.ContactIsPrimary,
            SegmentAttributeCatalog.ContactAccountType
        });
        var needsTerritory = usedCodes.Overlaps(new[]
        {
            SegmentAttributeCatalog.TerritoryHasCoverage,
            SegmentAttributeCatalog.TerritoryNode,
            SegmentAttributeCatalog.TerritoryModel
        });

        IReadOnlyList<SegmentLinkProjection> links = Array.Empty<SegmentLinkProjection>();
        if (needsLinks || (needsTerritory && isContactSegment))
        {
            links = await _candidates.LoadLinksAsync(
                tenantId, segment.SubjectType, subjectIds, cancellationToken);
        }

        var linksBySubject = links.GroupBy(l => l.ContactId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<SegmentLinkProjection>)g.ToList());

        if (needsLinks)
        {
            foreach (var candidate in candidates)
            {
                ApplyJoin(predicates, candidate, sets[candidate.SubjectId],
                    linksBySubject.GetValueOrDefault(candidate.SubjectId, Array.Empty<SegmentLinkProjection>()));
            }
        }

        // ---- Phase 2a: ONE bulk account-attribute read ----
        if (usedCodes.Contains(SegmentAttributeCatalog.AccountAttribute))
        {
            var attributes = await _candidates.LoadAccountAttributesAsync(tenantId, subjectIds, cancellationToken);
            var byAccount = attributes.GroupBy(a => a.AccountId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SegmentAccountAttributeProjection>)g.ToList());

            foreach (var candidate in candidates)
            {
                ApplyAccountAttributes(predicates, sets[candidate.SubjectId],
                    byAccount.GetValueOrDefault(candidate.SubjectId,
                        Array.Empty<SegmentAccountAttributeProjection>()));
            }
        }

        // ---- Phase 2b: ONE bulk territory read ----
        if (needsTerritory)
        {
            var accountIds = isContactSegment
                ? links.Select(l => l.AccountId).Distinct().ToList()
                : subjectIds;

            var coverage = await _territory.LoadAsync(tenantId, accountIds, effectiveAt, cancellationToken);
            var byAccount = coverage.Coverage.GroupBy(c => c.AccountId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<SegmentCoverageProjection>)g.ToList());

            foreach (var candidate in candidates)
            {
                var ownAccounts = isContactSegment
                    ? linksBySubject.GetValueOrDefault(candidate.SubjectId, Array.Empty<SegmentLinkProjection>())
                        .Select(l => l.AccountId).Distinct().ToList()
                    : new List<Guid> { candidate.SubjectId };

                ApplyTerritory(predicates, sets[candidate.SubjectId], coverage.CoverageAvailable,
                    ownAccounts.SelectMany(a => byAccount.GetValueOrDefault(
                        a, Array.Empty<SegmentCoverageProjection>())).ToList());
            }
        }

        // ---- Phase 2c: ONE bulk consent read, then the MOD-0164 engine in memory (no I/O per candidate) ----
        if (usedCodes.Contains(SegmentAttributeCatalog.ConsentEligibility)
            || usedCodes.Contains(SegmentAttributeCatalog.ConsentScopeProduct)
            || usedCodes.Contains(SegmentAttributeCatalog.ConsentScopeBrand))
        {
            var snapshot = await _consent.LoadAsync(tenantId, segment.SubjectType, subjectIds, cancellationToken);
            var consentsBySubject = snapshot.Consents.GroupBy(c => c.SubjectId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ConsentRecord>)g.ToList());
            var preferencesBySubject = snapshot.Preferences.GroupBy(p => p.SubjectId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<PreferenceRecord>)g.ToList());

            foreach (var candidate in candidates)
            {
                ApplyConsent(predicates, segment.SubjectType, candidate, sets[candidate.SubjectId],
                    consentsBySubject.GetValueOrDefault(candidate.SubjectId, Array.Empty<ConsentRecord>()),
                    preferencesBySubject.GetValueOrDefault(candidate.SubjectId, Array.Empty<PreferenceRecord>()),
                    effectiveAt);
            }
        }

        // ---- Phase 2d: concept.affinity — derived ONCE per predicate, applied to every candidate ----
        if (usedCodes.Contains(SegmentAttributeCatalog.ConceptAffinity))
        {
            await ApplyConceptAffinityAsync(
                tenantId, predicates, candidates, sets, effectiveAt, cancellationToken);
        }

        return new SegmentAttributeContext(sets);
    }

    private static void ApplyNative(
        IReadOnlyList<SegmentCriteriaNode> predicates, SegmentSubjectSnapshot candidate, SegmentAttributeValueSet set)
    {
        foreach (var node in predicates)
        {
            var code = (node.AttributeCode ?? string.Empty).Trim().ToLowerInvariant();
            switch (code)
            {
                case SegmentAttributeCatalog.AccountType:
                case SegmentAttributeCatalog.ContactType:
                    set.SetValue(node.NodeId, candidate.Type);
                    break;
                case SegmentAttributeCatalog.AccountCategory:
                    set.SetValue(node.NodeId, candidate.Category);
                    break;
                case SegmentAttributeCatalog.AccountStatus:
                case SegmentAttributeCatalog.ContactStatus:
                    set.SetValue(node.NodeId, candidate.Status);
                    break;
                case SegmentAttributeCatalog.AccountCountry:
                case SegmentAttributeCatalog.ContactCountry:
                    set.SetValue(node.NodeId, candidate.Country);
                    break;
                case SegmentAttributeCatalog.AccountCity:
                case SegmentAttributeCatalog.ContactCity:
                    set.SetValue(node.NodeId, candidate.City);
                    break;
                case SegmentAttributeCatalog.AccountDistrict:
                case SegmentAttributeCatalog.ContactDistrict:
                    set.SetValue(node.NodeId, candidate.District);
                    break;
                case SegmentAttributeCatalog.AccountParentAccount:
                    set.SetValue(node.NodeId, candidate.ParentAccountId?.ToString());
                    break;
                case SegmentAttributeCatalog.AccountCreatedAt:
                case SegmentAttributeCatalog.ContactCreatedAt:
                    set.SetValue(node.NodeId, candidate.CreatedAt.ToString("O"));
                    break;
                case SegmentAttributeCatalog.ContactSpecialty:
                    set.SetValue(node.NodeId, candidate.Specialty);
                    break;
                case SegmentAttributeCatalog.ContactProfessionalTitle:
                    set.SetValue(node.NodeId, candidate.ProfessionalTitle);
                    break;
                case SegmentAttributeCatalog.ContactDepartment:
                    set.SetValue(node.NodeId, candidate.Department);
                    break;
                case SegmentAttributeCatalog.ContactGender:
                    set.SetValue(node.NodeId, candidate.Gender);
                    break;
                case SegmentAttributeCatalog.ContactPreferredLanguage:
                    set.SetValue(node.NodeId, candidate.PreferredLanguage);
                    break;
            }
        }
    }

    private static void ApplyJoin(
        IReadOnlyList<SegmentCriteriaNode> predicates,
        SegmentSubjectSnapshot candidate,
        SegmentAttributeValueSet set,
        IReadOnlyList<SegmentLinkProjection> links)
    {
        foreach (var node in predicates)
        {
            switch ((node.AttributeCode ?? string.Empty).Trim().ToLowerInvariant())
            {
                case SegmentAttributeCatalog.ContactAccountRole:
                    set.SetValues(node.NodeId, links.Select(l => (string?)l.RoleCode));
                    break;
                case SegmentAttributeCatalog.ContactIsPrimary:
                    set.SetValues(node.NodeId,
                        links.Select(l => (string?)(l.IsPrimary ? "true" : "false")));
                    break;
                case SegmentAttributeCatalog.ContactAccountType:
                    set.SetValues(node.NodeId, links.Select(l => l.AccountType));
                    break;
            }
        }
    }

    private static void ApplyAccountAttributes(
        IReadOnlyList<SegmentCriteriaNode> predicates,
        SegmentAttributeValueSet set,
        IReadOnlyList<SegmentAccountAttributeProjection> attributes)
    {
        foreach (var node in predicates.Where(n => string.Equals(
                     (n.AttributeCode ?? string.Empty).Trim().ToLowerInvariant(),
                     SegmentAttributeCatalog.AccountAttribute, StringComparison.Ordinal)))
        {
            var key = node.Parameters.GetValueOrDefault(SegmentAttributeCatalog.ParameterAttributeCode) ?? string.Empty;
            set.SetValues(node.NodeId, attributes
                .Where(a => string.Equals(a.AttributeCode, key.Trim(), StringComparison.OrdinalIgnoreCase))
                .Select(a => a.Value));
        }
    }

    private static void ApplyTerritory(
        IReadOnlyList<SegmentCriteriaNode> predicates,
        SegmentAttributeValueSet set,
        bool coverageAvailable,
        IReadOnlyList<SegmentCoverageProjection> coverage)
    {
        foreach (var node in predicates)
        {
            var code = (node.AttributeCode ?? string.Empty).Trim().ToLowerInvariant();
            if (code is not (SegmentAttributeCatalog.TerritoryHasCoverage
                or SegmentAttributeCatalog.TerritoryNode
                or SegmentAttributeCatalog.TerritoryModel))
            {
                continue;
            }

            if (!coverageAvailable)
            {
                // No operationally valid model: coverage cannot be answered. Eliminate with the reason, complete the
                // resolution, invent nothing.
                set.MarkUnresolved(node.NodeId, SegmentReasonCodes.TerritoryCoverageUnavailable);
                continue;
            }

            switch (code)
            {
                case SegmentAttributeCatalog.TerritoryHasCoverage:
                    set.SetValue(node.NodeId, coverage.Count > 0 ? "true" : "false");
                    break;
                case SegmentAttributeCatalog.TerritoryNode:
                    set.SetValues(node.NodeId,
                        coverage.Select(c => (string?)c.TerritoryNodeId.ToString()));
                    break;
                case SegmentAttributeCatalog.TerritoryModel:
                    set.SetValues(node.NodeId,
                        coverage.Where(c => c.TerritoryModelId != Guid.Empty)
                            .Select(c => (string?)c.TerritoryModelId.ToString()));
                    break;
            }
        }
    }

    private static void ApplyConsent(
        IReadOnlyList<SegmentCriteriaNode> predicates,
        string subjectType,
        SegmentSubjectSnapshot candidate,
        SegmentAttributeValueSet set,
        IReadOnlyList<ConsentRecord> consents,
        IReadOnlyList<PreferenceRecord> preferences,
        DateTimeOffset effectiveAt)
    {
        foreach (var node in predicates)
        {
            var code = (node.AttributeCode ?? string.Empty).Trim().ToLowerInvariant();
            if (code is not (SegmentAttributeCatalog.ConsentEligibility
                or SegmentAttributeCatalog.ConsentScopeProduct
                or SegmentAttributeCatalog.ConsentScopeBrand))
            {
                continue;
            }

            if (code is SegmentAttributeCatalog.ConsentScopeProduct or SegmentAttributeCatalog.ConsentScopeBrand)
            {
                var scopeType = code == SegmentAttributeCatalog.ConsentScopeProduct ? "product" : "brand";
                set.SetValues(node.NodeId, consents
                    .Where(c => !c.IsArchived()
                                && c.IsEffectiveAt(effectiveAt)
                                && string.Equals(c.ScopeType, scopeType, StringComparison.OrdinalIgnoreCase)
                                && c.ScopeId is not null)
                    .Select(c => (string?)c.ScopeId!.Value.ToString()));
                continue;
            }

            var request = new ConsentEvaluationRequest(
                subjectType,
                candidate.SubjectId,
                node.Parameters.GetValueOrDefault(SegmentAttributeCatalog.ParameterChannel) ?? string.Empty,
                node.Parameters.GetValueOrDefault(SegmentAttributeCatalog.ParameterPurpose) ?? string.Empty,
                effectiveAt,
                IncludeDiagnostics: false);

            // The MOD-0164 engine, unchanged and in memory: consent semantics can never drift between the modules.
            var result = ConsentEvaluationEngine.Evaluate(request, consents, preferences, effectiveAt);
            var status = result.EligibilityStatus;

            var asksForUnknown = node.Values.Any(v =>
                string.Equals(v?.Trim(), ConsentEligibilityStatus.Unknown, StringComparison.OrdinalIgnoreCase));

            if (string.Equals(status, ConsentEligibilityStatus.Unknown, StringComparison.Ordinal) && !asksForUnknown)
            {
                // unknown is NEVER allowed. Unless the author explicitly asked for it, it eliminates the candidate.
                set.MarkUnresolved(node.NodeId, SegmentReasonCodes.ConsentUnknown);
                continue;
            }

            set.SetValue(node.NodeId, status);
            if (string.Equals(status, ConsentEligibilityStatus.Blocked, StringComparison.Ordinal))
            {
                set.SetAdvisory(node.NodeId, SegmentReasonCodes.ConsentBlocked);
            }
        }
    }

    private async Task ApplyConceptAffinityAsync(
        Guid tenantId,
        IReadOnlyList<SegmentCriteriaNode> predicates,
        IReadOnlyList<SegmentSubjectSnapshot> candidates,
        IReadOnlyDictionary<Guid, SegmentAttributeValueSet> sets,
        DateTimeOffset effectiveAt,
        CancellationToken cancellationToken)
    {
        foreach (var node in predicates.Where(n => string.Equals(
                     (n.AttributeCode ?? string.Empty).Trim().ToLowerInvariant(),
                     SegmentAttributeCatalog.ConceptAffinity, StringComparison.Ordinal)))
        {
            var depth = SegmentValidation.ResolveConceptAffinityDepth(node.Parameters);
            Guid? conceptSubjectId =
                node.Parameters.TryGetValue(SegmentAttributeCatalog.ParameterSubjectId, out var rawSubject)
                && Guid.TryParse(rawSubject, out var parsedSubject)
                    ? parsedSubject
                    : null;

            // The reachable specialty set is derived ONCE for this predicate and applied to every candidate; its cost
            // depends on the graph, never on the candidate count.
            var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var anyProductNodeFound = false;

            foreach (var product in node.Values.Where(v => !string.IsNullOrWhiteSpace(v)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var affinity = await _affinity.ResolveSpecialtiesAsync(
                    tenantId, product, depth, conceptSubjectId, effectiveAt, cancellationToken);
                anyProductNodeFound |= affinity.ProductNodeFound;
                foreach (var specialty in affinity.SpecialtyCodes)
                {
                    reachable.Add(specialty);
                }
            }

            foreach (var candidate in candidates)
            {
                var set = sets[candidate.SubjectId];

                if (!anyProductNodeFound)
                {
                    // The graph knows nothing about the product: empty answer with its own reason, never a 503 and
                    // never "everyone matches".
                    set.MarkUnresolved(node.NodeId, SegmentReasonCodes.ConceptProductNodeMissing);
                    continue;
                }

                if (reachable.Count == 0)
                {
                    set.MarkUnresolved(node.NodeId, SegmentReasonCodes.ConceptAffinityNoSpecialtyReached);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(candidate.Specialty))
                {
                    // A blank specialty is never a match.
                    set.MarkUnresolved(node.NodeId, SegmentReasonCodes.ConceptSubjectSpecialtyMissing);
                    continue;
                }

                var matched = reachable.Contains(candidate.Specialty.Trim());

                // The predicate compares the candidate specialty against the reachable set; feeding the evaluator the
                // requested value on a hit and nothing on a miss keeps the operator semantics (eq / in) intact.
                set.SetValues(node.NodeId, matched ? node.Values.Select(v => (string?)v) : Array.Empty<string?>());
                if (!matched)
                {
                    set.SetAdvisory(node.NodeId, SegmentReasonCodes.ConceptAffinityNotMatched);
                }
            }
        }
    }
}
