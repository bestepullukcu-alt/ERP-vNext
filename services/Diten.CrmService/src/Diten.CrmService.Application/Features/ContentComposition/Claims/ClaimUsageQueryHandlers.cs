using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Common.ReferenceValidation;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>
/// WP-CL-BE-6 — the claim usage read. Every repository call is tenant-first; nothing is written.
/// <list type="bullet">
/// <item><b>content</b>: non-archived knowledge contents with a <see cref="KnowledgeContent.ClaimRefs"/> entry for the
/// code; grouped by the ref's CountryCode, GLOBAL for a core ref.</item>
/// <item><b>content-set</b>: non-archived sets whose <see cref="ContentSet.SelectedClaims"/> hold any record id of the
/// code; grouped by the set's ContentScope MarketRefs (one group each). MarketRefs are opaque strings: a ref counts as a
/// country only when it is a published <c>COUNTRY_CODES</c> value or a country this claim already has a version in;
/// anything else (e.g. a region like <c>eu</c>) — or a set without scope / market — lands in GLOBAL. Country lists are
/// never hardcoded here.</item>
/// <item><b>journey</b>: non-archived journeys with an active stage whose recommended knowledge path (the pinned path
/// id, or — for a <c>latest-published</c> stage — any path of that code) has an active step pointing at one of the
/// matched contents (by id, or by code for a <c>latest-published</c> step). <c>via</c> = that content's code; the group
/// comes from the content.</item>
/// </list>
/// </summary>
public sealed class GetClaimUsageHandler : IRequestHandler<GetClaimUsageQuery, Response<ClaimUsageDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IClaimRepository _claims;
    private readonly IClaimCountryVersionRepository _versions;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IContentSetRepository _sets;
    private readonly IContentScopeRepository _scopes;
    private readonly IKnowledgePathRepository _paths;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IReferenceDataCatalogReader? _catalog;

    public GetClaimUsageHandler(
        ITenantContext tenant,
        IClaimRepository claims,
        IClaimCountryVersionRepository versions,
        IKnowledgeContentRepository contents,
        IContentSetRepository sets,
        IContentScopeRepository scopes,
        IKnowledgePathRepository paths,
        IContentEngagementJourneyRepository journeys,
        IReferenceDataCatalogReader? catalog = null)
    {
        _tenant = tenant;
        _claims = claims;
        _versions = versions;
        _contents = contents;
        _sets = sets;
        _scopes = scopes;
        _paths = paths;
        _journeys = journeys;
        _catalog = catalog;
    }

    public async Task<Response<ClaimUsageDto>> Handle(GetClaimUsageQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ClaimUsageDto>.Fail("Tenant context is required.", 400);
        }

        var claimCode = request.ClaimCode?.Trim();
        if (string.IsNullOrEmpty(claimCode))
        {
            return Response<ClaimUsageDto>.Fail(new[] { "required", "claimCode is required." }, 400);
        }

        var countryFilter = string.IsNullOrWhiteSpace(request.CountryCode)
            ? null
            : request.CountryCode.Trim().ToUpperInvariant();

        var records = await _claims.ListByCodeAsync(tenantId, claimCode, cancellationToken);
        var recordsById = records.ToDictionary(c => c.Id);
        var versions = (await _versions.ListByClaimCodeAsync(tenantId, claimCode, cancellationToken))
            .ToDictionary(v => v.Id);

        var groups = new Dictionary<string, List<ClaimUsageItemDto>>(StringComparer.Ordinal);
        void Add(string group, ClaimUsageItemDto item)
        {
            if (countryFilter is not null && !string.Equals(group, countryFilter, StringComparison.Ordinal))
            {
                return;
            }

            if (!groups.TryGetValue(group, out var items))
            {
                groups[group] = items = new List<ClaimUsageItemDto>();
            }

            if (!items.Any(i => i.Type == item.Type && i.Id == item.Id && i.Via == item.Via))
            {
                items.Add(item);
            }
        }

        // ---- contents ------------------------------------------------------------------------------------------
        // content id → (content, groups it counts in with the review flag per group)
        var matched = new Dictionary<Guid, (KnowledgeContent Content, List<(string Group, bool NeedsReview)> Groups)>();
        foreach (var content in (await _contents.ListByClaimCodeAsync(tenantId, claimCode, cancellationToken))
                     .Where(c => !c.IsArchived()))
        {
            var contentGroups = new List<(string Group, bool NeedsReview)>();
            foreach (var r in content.ClaimRefs.Where(r =>
                         string.Equals(r.ClaimCode, claimCode, StringComparison.Ordinal)))
            {
                var group = string.IsNullOrWhiteSpace(r.CountryCode)
                    ? ClaimUsageGroups.Global
                    : r.CountryCode.Trim().ToUpperInvariant();
                var needsReview = r.CountryVersionId is { } vid
                    ? versions.TryGetValue(vid, out var v) && IsReviewRequired(v.Status)
                    : recordsById.TryGetValue(r.ClaimId, out var c) && IsReviewRequired(c.Status);
                contentGroups.Add((group, needsReview));
                Add(group, new ClaimUsageItemDto(ClaimUsageItemTypes.Content, content.Id, content.ContentCode,
                    content.ContentTitle, content.LanguageCode, content.ContentVersion, content.ContentStatus, null,
                    needsReview));
            }

            if (contentGroups.Count > 0)
            {
                matched[content.Id] = (content, contentGroups);
            }
        }

        // ---- content sets --------------------------------------------------------------------------------------
        if (recordsById.Count > 0)
        {
            HashSet<string>? knownCountries = null;
            foreach (var set in (await _sets.ListAsync(tenantId, cancellationToken)).Where(s => !s.IsArchived()))
            {
                var selections = set.SelectedClaims.Where(sc => recordsById.ContainsKey(sc.ClaimId)).ToList();
                if (selections.Count == 0)
                {
                    continue;
                }

                var selected = selections.Select(sc => recordsById[sc.ClaimId]).ToList();

                var scope = set.Scope is { } scopeRef && scopeRef.ContentScopeId != Guid.Empty
                    ? await _scopes.GetByIdAsync(tenantId, scopeRef.ContentScopeId, cancellationToken)
                    : null;
                knownCountries ??= await KnownCountriesAsync(versions.Values, cancellationToken);
                var setGroups = MarketGroups(scope?.MarketRefs, knownCountries);
                var selectedNeedsReview = selected.Any(c => IsReviewRequired(c.Status));
                // The claim version the set pinned at selection time (falls back to the record's own version).
                var version = selections
                    .Select(sc => string.IsNullOrWhiteSpace(sc.ClaimVersion) ? recordsById[sc.ClaimId].ClaimVersion : sc.ClaimVersion)
                    .FirstOrDefault();
                foreach (var group in setGroups)
                {
                    var needsReview = group == ClaimUsageGroups.Global
                        ? selectedNeedsReview
                        : selectedNeedsReview || versions.Values.Any(v =>
                            string.Equals(v.CountryCode, group, StringComparison.OrdinalIgnoreCase)
                            && !v.IsArchived() && IsReviewRequired(v.Status));
                    Add(group, new ClaimUsageItemDto(ClaimUsageItemTypes.ContentSet, set.Id, set.SetCode, set.SetName,
                        null, version, set.Status, null, needsReview));
                }
            }
        }

        // ---- journeys (stage → recommended path → step → matched content) -------------------------------------
        if (matched.Count > 0)
        {
            var codes = matched.Values
                .GroupBy(m => m.Content.ContentCode, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First().Content.Id, StringComparer.Ordinal);

            // path id → matched content ids it steps through
            var pathHits = new Dictionary<Guid, (KnowledgePath Path, List<Guid> ContentIds)>();
            foreach (var path in (await _paths.ListAsync(tenantId, cancellationToken)).Where(p => !p.IsArchived()))
            {
                var hits = new List<Guid>();
                foreach (var step in path.ActiveSteps())
                {
                    if (matched.ContainsKey(step.ContentId))
                    {
                        hits.Add(step.ContentId);
                    }
                    else if (string.Equals(step.VersionPinPolicy, KnowledgePathVersionPin.LatestPublished,
                                 StringComparison.OrdinalIgnoreCase)
                             && codes.TryGetValue(step.ContentCode, out var byCode))
                    {
                        hits.Add(byCode);
                    }
                }

                if (hits.Count > 0)
                {
                    pathHits[path.Id] = (path, hits.Distinct().ToList());
                }
            }

            if (pathHits.Count > 0)
            {
                foreach (var journey in (await _journeys.ListAsync(tenantId, cancellationToken))
                             .Where(j => !j.IsArchived()))
                {
                    foreach (var stage in journey.ActiveStages())
                    {
                        var stagePaths = pathHits.Values.Where(p =>
                            p.Path.Id == stage.RecommendedKnowledgePathId
                            || (string.Equals(stage.PathVersionPinPolicy, ContentEngagementJourneyPathPin.LatestPublished,
                                    StringComparison.OrdinalIgnoreCase)
                                && string.Equals(p.Path.PathCode, stage.PathCode, StringComparison.Ordinal)));
                        foreach (var contentId in stagePaths.SelectMany(p => p.ContentIds).Distinct())
                        {
                            var (content, contentGroups) = matched[contentId];
                            foreach (var (group, needsReview) in contentGroups)
                            {
                                Add(group, new ClaimUsageItemDto(ClaimUsageItemTypes.Journey, journey.Id,
                                    journey.JourneyCode, journey.JourneyName, journey.LanguageCode,
                                    journey.JourneyVersion, journey.JourneyStatus, content.ContentCode, needsReview));
                            }
                        }
                    }
                }
            }
        }

        var ordered = groups
            .OrderBy(g => g.Key == ClaimUsageGroups.Global ? 0 : 1)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new ClaimUsageGroupDto(g.Key, g.Value
                .OrderBy(i => TypeOrder(i.Type))
                .ThenBy(i => i.Code, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Via, StringComparer.OrdinalIgnoreCase)
                .ToList()))
            .ToList();

        return Response<ClaimUsageDto>.Success(new ClaimUsageDto(
            records.FirstOrDefault()?.ClaimCode ?? claimCode, ordered));
    }

    private static bool IsReviewRequired(string? status)
        => string.Equals(status, ClaimStatuses.ReviewRequired, StringComparison.OrdinalIgnoreCase);

    private static int TypeOrder(string type) => type switch
    {
        ClaimUsageItemTypes.Content => 0,
        ClaimUsageItemTypes.ContentSet => 1,
        _ => 2
    };

    /// <summary>The country axis for market-ref mapping: the published <c>COUNTRY_CODES</c> values (when the catalog is
    /// reachable) plus the countries this claim already has versions in (validated against the same set on write).</summary>
    private async Task<HashSet<string>> KnownCountriesAsync(
        IEnumerable<ClaimCountryVersion> versions, CancellationToken cancellationToken)
    {
        var known = new HashSet<string>(
            versions.Select(v => v.CountryCode.Trim().ToUpperInvariant()), StringComparer.Ordinal);
        if (_catalog is null)
        {
            return known;
        }

        try
        {
            var set = await _catalog.GetPublishedValuesAsync(ClaimReferenceSets.CountryCodes, cancellationToken);
            if (set.IsPublished)
            {
                foreach (var value in set.Values.Where(v => v.IsActive))
                {
                    known.Add(value.ValueCode.Trim().ToUpperInvariant());
                }
            }
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            // Read-only grouping hint: an unreachable catalog degrades to the claim's own countries (unmapped → GLOBAL).
        }

        return known;
    }

    /// <summary>ContentScope MarketRefs are opaque config strings. A ref that is a known country code (see
    /// <see cref="KnownCountriesAsync"/>) becomes that country's group (upper-cased); anything else (e.g. a region like
    /// <c>eu</c>) cannot be mapped and counts as GLOBAL, as does an empty list.</summary>
    public static IReadOnlyList<string> MarketGroups(
        IReadOnlyList<string>? marketRefs, IReadOnlySet<string> knownCountries)
    {
        var result = new List<string>();
        foreach (var raw in marketRefs ?? Array.Empty<string>())
        {
            var value = raw?.Trim().ToUpperInvariant() ?? string.Empty;
            var group = knownCountries.Contains(value) ? value : ClaimUsageGroups.Global;
            if (!result.Contains(group))
            {
                result.Add(group);
            }
        }

        if (result.Count == 0)
        {
            result.Add(ClaimUsageGroups.Global);
        }

        return result;
    }
}
