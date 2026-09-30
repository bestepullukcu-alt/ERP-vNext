using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
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
/// code; grouped by the set's own <see cref="ContentSet.CountryCode"/> (WP-SB-1R — validated against COUNTRY_CODES on
/// write); a pre-SB-1R set without a country lands in GLOBAL.</item>
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
    private readonly IKnowledgePathRepository _paths;
    private readonly IContentEngagementJourneyRepository _journeys;

    public GetClaimUsageHandler(
        ITenantContext tenant,
        IClaimRepository claims,
        IClaimCountryVersionRepository versions,
        IKnowledgeContentRepository contents,
        IContentSetRepository sets,
        IKnowledgePathRepository paths,
        IContentEngagementJourneyRepository journeys)
    {
        _tenant = tenant;
        _claims = claims;
        _versions = versions;
        _contents = contents;
        _sets = sets;
        _paths = paths;
        _journeys = journeys;
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
            foreach (var set in (await _sets.ListAsync(tenantId, cancellationToken)).Where(s => !s.IsArchived()))
            {
                var selections = set.SelectedClaims.Where(sc => recordsById.ContainsKey(sc.ClaimId)).ToList();
                if (selections.Count == 0)
                {
                    continue;
                }

                var selected = selections.Select(sc => recordsById[sc.ClaimId]).ToList();

                var setGroups = new[] { SetGroup(set) };
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

    /// <summary>WP-SB-1R — a content set groups under its own country (a COUNTRY_CODES value, validated on write); a
    /// pre-SB-1R set without a country groups under GLOBAL. The retired ContentScope MarketRefs are no longer read.</summary>
    public static string SetGroup(ContentSet set)
        => string.IsNullOrWhiteSpace(set.CountryCode)
            ? ClaimUsageGroups.Global
            : set.CountryCode.Trim().ToUpperInvariant();
}
