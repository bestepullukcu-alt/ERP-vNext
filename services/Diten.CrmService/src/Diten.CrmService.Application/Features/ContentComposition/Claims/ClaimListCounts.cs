using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;

namespace Diten.CrmService.Application.Features.ContentComposition.Claims;

/// <summary>
/// WP-CL-FE-1 — the list-row counters of <c>GET claims?includeCounts=true</c>. Read-only and page-scoped: every source
/// is read ONCE for the whole page (never per row), and any counter that cannot be computed is null — the list itself
/// never fails because of a counter.
/// <list type="bullet">
/// <item><c>evidenceCount</c>: the record's OWN active MOD-0031 links, from the page's single bulk evidence read
/// (the read-time evidence snapshot); null when MOD-0031 is unavailable.</item>
/// <item><c>approvedCountryCount</c>: countries of the claim code with a live approved or review-required version.</item>
/// <item><c>usageCount</c>: distinct contents + content sets + journeys using the claim code (the WP-CL-BE-6 usage
/// rules, evaluated in memory over one tenant read of each source).</item>
/// <item><c>expiringCountryCodes</c>: countries whose live version's validity ends within the expiring window.</item>
/// </list>
/// </summary>
public static class ClaimListCounts
{
    public static IReadOnlyDictionary<string, int> ApprovedCountries(IEnumerable<ClaimCountryVersion> versions) =>
        versions.Where(v => v.IsLive() && v.Status is ClaimStatuses.Approved or ClaimStatuses.ReviewRequired)
            .GroupBy(v => v.ClaimCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => g.Select(v => ClaimV2Checks.NormalizeCountry(v.CountryCode)).Distinct().Count(),
                StringComparer.Ordinal);

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> ExpiringCountries(
        IEnumerable<ClaimCountryVersion> versions, DateTimeOffset now, int windowDays) =>
        versions.Where(v => v.IsLive() && v.Status is ClaimStatuses.Approved or ClaimStatuses.ReviewRequired
                            && ClaimLines.IsExpiring(v, now, windowDays))
            .GroupBy(v => v.ClaimCode, StringComparer.Ordinal)
            .ToDictionary(g => g.Key,
                g => (IReadOnlyList<string>)g.Select(v => ClaimV2Checks.NormalizeCountry(v.CountryCode)).Distinct()
                    .OrderBy(c => c, StringComparer.Ordinal).ToList(),
                StringComparer.Ordinal);

    /// <summary>Usage per claim code (distinct contents + sets + journeys). One tenant read per source.</summary>
    public static async Task<IReadOnlyDictionary<string, int>> UsageAsync(
        Guid tenantId,
        IReadOnlyCollection<Claim> allClaims,
        IKnowledgeContentRepository contentsRepo,
        IContentSetRepository setsRepo,
        IKnowledgePathRepository pathsRepo,
        IContentEngagementJourneyRepository journeysRepo,
        CancellationToken ct)
    {
        var used = new Dictionary<string, HashSet<(string Type, Guid Id)>>(StringComparer.Ordinal);
        void Add(string code, string type, Guid id)
        {
            if (!used.TryGetValue(code, out var set))
            {
                used[code] = set = new HashSet<(string, Guid)>();
            }

            set.Add((type, id));
        }

        // contents: code → matched contents (non-archived with a ClaimRef of the code)
        var contentsByCode = new Dictionary<string, List<KnowledgeContent>>(StringComparer.Ordinal);
        foreach (var content in (await contentsRepo.ListAsync(tenantId, ct)).Where(c => !c.IsArchived()))
        {
            foreach (var code in content.ClaimRefs.Select(r => r.ClaimCode).Where(c => !string.IsNullOrWhiteSpace(c))
                         .Distinct(StringComparer.Ordinal))
            {
                if (!contentsByCode.TryGetValue(code, out var list))
                {
                    contentsByCode[code] = list = new List<KnowledgeContent>();
                }

                list.Add(content);
                Add(code, ClaimUsageItemTypes.Content, content.Id);
            }
        }

        // content sets: a selected claim record id → its code
        var codeById = allClaims.ToDictionary(c => c.Id, c => c.ClaimCode);
        foreach (var set in (await setsRepo.ListAsync(tenantId, ct)).Where(s => !s.IsArchived()))
        {
            foreach (var code in set.SelectedClaims
                         .Select(sc => codeById.TryGetValue(sc.ClaimId, out var code) ? code : null)
                         .OfType<string>().Distinct(StringComparer.Ordinal))
            {
                Add(code, ClaimUsageItemTypes.ContentSet, set.Id);
            }
        }

        // journeys: stage → recommended path → active step → a matched content (BE-6 rule, per claim code)
        if (contentsByCode.Count > 0)
        {
            var paths = (await pathsRepo.ListAsync(tenantId, ct)).Where(p => !p.IsArchived()).ToList();
            var journeys = (await journeysRepo.ListAsync(tenantId, ct)).Where(j => !j.IsArchived()).ToList();
            foreach (var (code, contents) in contentsByCode)
            {
                var ids = contents.Select(c => c.Id).ToHashSet();
                var contentCodes = contents.Select(c => c.ContentCode).ToHashSet(StringComparer.Ordinal);
                var hitPaths = paths.Where(p => p.ActiveSteps().Any(step => ids.Contains(step.ContentId)
                        || (string.Equals(step.VersionPinPolicy, KnowledgePathVersionPin.LatestPublished,
                                StringComparison.OrdinalIgnoreCase)
                            && contentCodes.Contains(step.ContentCode))))
                    .ToList();
                if (hitPaths.Count == 0)
                {
                    continue;
                }

                foreach (var journey in journeys)
                {
                    if (journey.ActiveStages().Any(stage => hitPaths.Any(p =>
                            p.Id == stage.RecommendedKnowledgePathId
                            || (string.Equals(stage.PathVersionPinPolicy, ContentEngagementJourneyPathPin.LatestPublished,
                                    StringComparison.OrdinalIgnoreCase)
                                && string.Equals(p.PathCode, stage.PathCode, StringComparison.Ordinal)))))
                    {
                        Add(code, ClaimUsageItemTypes.Journey, journey.Id);
                    }
                }
            }
        }

        return used.ToDictionary(kv => kv.Key, kv => kv.Value.Count, StringComparer.Ordinal);
    }
}
