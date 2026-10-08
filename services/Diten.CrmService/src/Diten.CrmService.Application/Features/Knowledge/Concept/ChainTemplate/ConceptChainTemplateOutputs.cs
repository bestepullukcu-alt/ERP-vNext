using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Path.Release;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using PlannedVisitEntity = Diten.CrmService.Domain.Entities.PlannedVisit;

namespace Diten.CrmService.Application.Features.Knowledge.Concept.ChainTemplate;

// ---------------------------------------------------------------------------------------------------------------
// WP-KP-CH-1 (old SB-2b) — the chain template "Outputs" read: where a chain lands in the field.
//   chain template ──(KnowledgePath.ChainTemplate, KP-1 ChainRef)──▶ knowledge paths
//                  ──(journey stage, KP-3 KnowledgePathReleaseRules.UsesPath)──▶ engagement journeys
//                  ──(PlannedVisit.ContentItems[].PathId, SB-3b)──▶ planned visits (a COUNT only)
// READ ONLY: nothing is written, no collection or index is added. No person data leaves (visits are counted, never
// listed). A section the actor may not read in detail comes back as its count + Restricted (nothing invented).
// ---------------------------------------------------------------------------------------------------------------

/// <summary>WP-KP-CH-1 — the outputs of one chain template version. <paramref name="IncludeOtherVersions"/> adds the
/// paths bound to the other versions of the same ChainCode. The two Can* flags are the actor's detail permissions
/// (knowledge path read / journey read), decided by the API from the caller's claims.</summary>
public sealed record GetConceptChainTemplateOutputsQuery(
    Guid ConceptChainTemplateId,
    bool IncludeOtherVersions = false,
    bool CanReadPaths = true,
    bool CanReadJourneys = true) : IRequest<Response<ConceptChainTemplateOutputsDto>>;

public sealed record ConceptChainTemplateOutputsDto(
    Guid ConceptChainTemplateId,
    string ChainCode,
    string ChainVersion,
    bool IncludesOtherVersions,
    int PathCount,
    int CurrentReleaseCount,
    int JourneyCount,
    int PlannedVisitCount,
    bool PathsRestricted,
    bool JourneysRestricted,
    IReadOnlyList<ConceptChainOutputPathDto> Paths,
    IReadOnlyList<ConceptChainOutputJourneyDto> Journeys);

public sealed record ConceptChainOutputPathDto(
    Guid PathId,
    string PathCode,
    string PathName,
    string? CountryCode,
    string? LanguageCode,
    string PathVersion,
    string PathStatus,
    bool IsCurrentRelease,
    string ChainVersion,
    string? LatestRevisionStatus,
    int? LatestRevisionNumber);

public sealed record ConceptChainOutputJourneyDto(
    Guid JourneyId,
    string JourneyCode,
    string JourneyName,
    string JourneyStatus,
    string? LanguageCode,
    int StageCount);

/// <summary>WP-KP-CH-1 — the selection rules of the outputs read, in one place (the handler only fetches).</summary>
public static class ConceptChainOutputRules
{
    /// <summary>A path is an output of <paramref name="chainTemplateIds"/> when its KP-1 ChainRef points at one of them
    /// and it is not archived. A path without a ChainRef (legacy, unapproved) belongs to no chain.</summary>
    public static bool IsOutputPath(KnowledgePath path, IReadOnlySet<Guid> chainTemplateIds)
        => !path.IsArchived()
           && path.ChainTemplate is { } chainRef
           && chainTemplateIds.Contains(chainRef.ConceptChainTemplateId);

    /// <summary>A plan counts when it is on or after <paramref name="today"/>, neither cancelled nor archived, and one of
    /// its content items tells one of <paramref name="pathIds"/>.</summary>
    public static bool CountsAsPlannedVisit(PlannedVisitEntity visit, IReadOnlySet<Guid> pathIds, DateOnly today)
        => !visit.IsCancelled() && !visit.IsArchived() && visit.ArchivedAt is null
           && visit.PlannedDate >= today
           && visit.ContentItems.Any(item => pathIds.Contains(item.PathId));
}

public sealed class GetConceptChainTemplateOutputsHandler
    : IRequestHandler<GetConceptChainTemplateOutputsQuery, Response<ConceptChainTemplateOutputsDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IConceptChainTemplateRepository _templates;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgePathRevisionRepository _revisions;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly IPlannedVisitRepository _visits;
    private readonly TimeProvider _clock;

    public GetConceptChainTemplateOutputsHandler(
        ITenantContext tenant,
        IConceptChainTemplateRepository templates,
        IKnowledgePathRepository paths,
        IKnowledgePathRevisionRepository revisions,
        IContentEngagementJourneyRepository journeys,
        IPlannedVisitRepository visits,
        TimeProvider? clock = null)
    {
        _tenant = tenant;
        _templates = templates;
        _paths = paths;
        _revisions = revisions;
        _journeys = journeys;
        _visits = visits;
        _clock = clock ?? TimeProvider.System;
    }

    public async Task<Response<ConceptChainTemplateOutputsDto>> Handle(
        GetConceptChainTemplateOutputsQuery request, CancellationToken ct)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ConceptChainTemplateOutputsDto>.Fail("Tenant context is required.", 400);
        }

        var template = await _templates.GetByIdAsync(tenantId, request.ConceptChainTemplateId, ct);
        if (template is null)
        {
            return Response<ConceptChainTemplateOutputsDto>.Fail("Concept chain template not found.", 404);
        }

        // This version, or every version of the same ChainCode (same subject) when asked.
        var versions = request.IncludeOtherVersions
            ? (await _templates.ListByCodeAsync(tenantId, template.SubjectId, template.ChainCode, ct))
                .Where(t => t.TenantId == tenantId).ToList()
            : new List<ConceptChainTemplate> { template };
        if (versions.All(v => v.Id != template.Id)) versions.Add(template);
        var chainIds = versions.Select(v => v.Id).ToHashSet();

        var paths = (await _paths.ListAsync(tenantId, ct))
            .Where(p => ConceptChainOutputRules.IsOutputPath(p, chainIds))
            .OrderBy(p => p.CountryCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.LanguageCode ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.PathVersion, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.PathCode, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // KP-3's usage rule (pinned to the row, or following its code), over every non-archived journey — the same
        // read the path usage endpoint makes.
        var journeys = (await _journeys.ListAsync(tenantId, ct))
            .Where(j => !j.IsArchived())
            .Select(j => (Journey: j, Stages: j.ActiveStages()
                .Count(s => paths.Any(p => KnowledgePathReleaseRules.UsesPath(s, p, includeLatestPublished: true)))))
            .Where(x => x.Stages > 0)
            .OrderBy(x => x.Journey.JourneyCode, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ConceptChainOutputJourneyDto(x.Journey.Id, x.Journey.JourneyCode, x.Journey.JourneyName,
                x.Journey.JourneyStatus, x.Journey.LanguageCode, x.Stages))
            .ToList();

        var pathIds = paths.Select(p => p.Id).ToHashSet();
        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var plannedVisitCount = pathIds.Count == 0
            ? 0
            : (await _visits.ListFromDateByContentPathsAsync(tenantId, pathIds, today, ct))
                .Count(v => v.TenantId == tenantId && ConceptChainOutputRules.CountsAsPlannedVisit(v, pathIds, today));

        var pathDtos = new List<ConceptChainOutputPathDto>();
        if (request.CanReadPaths)
        {
            var chainVersions = versions.ToDictionary(v => v.Id, v => v.ChainVersion);
            foreach (var path in paths)
            {
                var latest = (await _revisions.ListByPathAsync(tenantId, path.Id, ct))
                    .OrderByDescending(r => r.RevisionNumber)
                    .FirstOrDefault();
                pathDtos.Add(new ConceptChainOutputPathDto(path.Id, path.PathCode, path.PathName, path.CountryCode,
                    path.LanguageCode, path.PathVersion, path.PathStatus, KnowledgePathReleaseRules.IsCurrentRelease(path),
                    chainVersions.TryGetValue(path.ChainTemplate!.ConceptChainTemplateId, out var v) ? v : path.ChainTemplate.ChainVersion,
                    latest?.Status, latest?.RevisionNumber));
            }
        }

        return Response<ConceptChainTemplateOutputsDto>.Success(new ConceptChainTemplateOutputsDto(
            template.Id,
            template.ChainCode,
            template.ChainVersion,
            request.IncludeOtherVersions,
            paths.Count,
            paths.Count(KnowledgePathReleaseRules.IsCurrentRelease),
            journeys.Count,
            plannedVisitCount,
            !request.CanReadPaths,
            !request.CanReadJourneys,
            pathDtos,
            request.CanReadJourneys ? journeys : Array.Empty<ConceptChainOutputJourneyDto>()));
    }
}
