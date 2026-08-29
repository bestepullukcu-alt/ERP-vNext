using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.ContentEngagementJourney.Handlers;

using JourneyEntity = Diten.CrmService.Domain.Entities.ContentEngagementJourney;

/// <summary>MOD-0162 FU05 read handlers. The KnowledgePath is resolved per stage (pinned / resolved-latest /
/// unresolved) and the status is always surfaced — never silent. The list projects the Stages array OUT
/// (large-document guard). No score / best-next / current-stage / advancement is computed.</summary>
public sealed class ListContentEngagementJourneysHandler
    : IRequestHandler<ListContentEngagementJourneysQuery, Response<ContentEngagementJourneyListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ContentEngagementJourneyPathResolver _pathResolver;

    public ListContentEngagementJourneysHandler(
        ITenantContext tenant, IContentEngagementJourneyRepository journeys,
        ContentEngagementJourneyPathResolver pathResolver)
    {
        _tenant = tenant;
        _journeys = journeys;
        _pathResolver = pathResolver;
    }

    public async Task<Response<ContentEngagementJourneyListDto>> Handle(
        ListContentEngagementJourneysQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentEngagementJourneyListDto>.Fail("Tenant context is required.", 400);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        IEnumerable<JourneyEntity> rows = string.IsNullOrWhiteSpace(request.JourneyCode)
            ? await _journeys.ListAsync(tenantId, cancellationToken)
            : await _journeys.ListByCodeAsync(tenantId, request.JourneyCode.Trim(), cancellationToken);

        if (request.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(x => x.SubjectId == subjectId);
        }

        if (request.TopicId is { } topicId && topicId != Guid.Empty)
        {
            rows = rows.Where(x => x.TopicId == topicId);
        }

        if (request.AudienceProfileId is { } profileId && profileId != Guid.Empty)
        {
            rows = rows.Where(x => x.AudienceProfileId == profileId);
        }

        if (!string.IsNullOrWhiteSpace(request.Language))
        {
            var lang = request.Language.Trim();
            rows = rows.Where(x => string.Equals(x.LanguageCode, lang, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = ContentEngagementJourneyStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.JourneyStatus == status);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.IsEffectiveAt(at));
        }

        // "Which journeys use this path?" — an in-memory scan of the embedded stages (the multikey index backs the
        // same question in Mongo). It never widens into a recommendation.
        if (request.KnowledgePathId is { } pathId && pathId != Guid.Empty)
        {
            rows = rows.Where(x => x.Stages.Any(s => !s.IsArchived() && s.RecommendedKnowledgePathId == pathId));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.JourneyName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.JourneyCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var list = rows.ToList();
        var ctx = await _pathResolver.BuildContextAsync(tenantId, cancellationToken);

        var items = list.Select(j => ContentEngagementJourneyMapper.ToListItem(j, ctx, effectiveAt)).ToList();
        return Response<ContentEngagementJourneyListDto>.Success(
            new ContentEngagementJourneyListDto(items, items.Count));
    }
}

public sealed class GetContentEngagementJourneyHandler
    : IRequestHandler<GetContentEngagementJourneyQuery, Response<ContentEngagementJourneyDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ContentEngagementJourneyPathResolver _pathResolver;

    public GetContentEngagementJourneyHandler(
        ITenantContext tenant, IContentEngagementJourneyRepository journeys,
        ContentEngagementJourneyPathResolver pathResolver)
    {
        _tenant = tenant;
        _journeys = journeys;
        _pathResolver = pathResolver;
    }

    public async Task<Response<ContentEngagementJourneyDto>> Handle(
        GetContentEngagementJourneyQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentEngagementJourneyDto>.Fail("Tenant context is required.", 400);
        }

        var journey = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Response<ContentEngagementJourneyDto>.Fail("Content engagement journey not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var ctx = await _pathResolver.BuildContextAsync(tenantId, cancellationToken);

        return Response<ContentEngagementJourneyDto>.Success(
            ContentEngagementJourneyMapper.ToDto(journey, ctx, effectiveAt));
    }
}

public sealed class GetContentEngagementJourneyStagesHandler
    : IRequestHandler<GetContentEngagementJourneyStagesQuery, Response<ContentEngagementJourneyStageListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IContentEngagementJourneyRepository _journeys;
    private readonly ContentEngagementJourneyPathResolver _pathResolver;

    public GetContentEngagementJourneyStagesHandler(
        ITenantContext tenant, IContentEngagementJourneyRepository journeys,
        ContentEngagementJourneyPathResolver pathResolver)
    {
        _tenant = tenant;
        _journeys = journeys;
        _pathResolver = pathResolver;
    }

    public async Task<Response<ContentEngagementJourneyStageListDto>> Handle(
        GetContentEngagementJourneyStagesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<ContentEngagementJourneyStageListDto>.Fail("Tenant context is required.", 400);
        }

        var journey = await _journeys.GetByIdAsync(tenantId, request.JourneyId, cancellationToken);
        if (journey is null)
        {
            return Response<ContentEngagementJourneyStageListDto>.Fail(
                "Content engagement journey not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var ctx = await _pathResolver.BuildContextAsync(tenantId, cancellationToken);

        var stages = ContentEngagementJourneyMapper.ToStageDtos(
            journey, ctx, effectiveAt, request.IncludeArchived);
        return Response<ContentEngagementJourneyStageListDto>.Success(
            new ContentEngagementJourneyStageListDto(stages, stages.Count));
    }
}
