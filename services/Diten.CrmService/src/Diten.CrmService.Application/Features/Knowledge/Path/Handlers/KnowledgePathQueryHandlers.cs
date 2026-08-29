using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Path.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Path.Handlers;

/// <summary>MOD-0162 FU04 read handlers. Content is resolved per step (pinned / latest-published / unresolved) and the
/// status is always surfaced — never silent. The list projects the Steps array OUT (large-document guard). No score /
/// best-next / recommendation is computed.</summary>
public sealed class ListKnowledgePathsHandler : IRequestHandler<ListKnowledgePathsQuery, Response<KnowledgePathListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;

    public ListKnowledgePathsHandler(
        ITenantContext tenant, IKnowledgePathRepository paths, IKnowledgeContentRepository contents,
        IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _paths = paths;
        _contents = contents;
        _nodes = nodes;
    }

    public async Task<Response<KnowledgePathListDto>> Handle(
        ListKnowledgePathsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgePathListDto>.Fail("Tenant context is required.", 400);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        IEnumerable<KnowledgePath> rows = string.IsNullOrWhiteSpace(request.PathCode)
            ? await _paths.ListAsync(tenantId, cancellationToken)
            : await _paths.ListByCodeAsync(tenantId, request.PathCode.Trim(), cancellationToken);

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
            var status = KnowledgePathStatuses.Normalize(request.Status);
            rows = rows.Where(x => x.PathStatus == status);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(x => x.IsEffectiveAt(at));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(x =>
                x.PathName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || x.PathCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(x => !x.IsArchived());
        }

        var list = rows.ToList();
        var ctx = new KnowledgePathMapper.ResolutionContext(
            await _contents.ListAsync(tenantId, cancellationToken),
            await _nodes.ListAsync(tenantId, cancellationToken));

        var items = list.Select(p => KnowledgePathMapper.ToListItem(p, ctx, effectiveAt)).ToList();
        return Response<KnowledgePathListDto>.Success(new KnowledgePathListDto(items, items.Count));
    }
}

public sealed class GetKnowledgePathHandler : IRequestHandler<GetKnowledgePathQuery, Response<KnowledgePathDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;

    public GetKnowledgePathHandler(
        ITenantContext tenant, IKnowledgePathRepository paths, IKnowledgeContentRepository contents,
        IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _paths = paths;
        _contents = contents;
        _nodes = nodes;
    }

    public async Task<Response<KnowledgePathDto>> Handle(
        GetKnowledgePathQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgePathDto>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<KnowledgePathDto>.Fail("Knowledge path not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var ctx = new KnowledgePathMapper.ResolutionContext(
            await _contents.ListAsync(tenantId, cancellationToken),
            await _nodes.ListAsync(tenantId, cancellationToken));

        return Response<KnowledgePathDto>.Success(KnowledgePathMapper.ToDto(path, ctx, effectiveAt));
    }
}

public sealed class GetKnowledgePathStepsHandler
    : IRequestHandler<GetKnowledgePathStepsQuery, Response<KnowledgePathStepListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgePathRepository _paths;
    private readonly IKnowledgeContentRepository _contents;
    private readonly IConceptNodeRepository _nodes;

    public GetKnowledgePathStepsHandler(
        ITenantContext tenant, IKnowledgePathRepository paths, IKnowledgeContentRepository contents,
        IConceptNodeRepository nodes)
    {
        _tenant = tenant;
        _paths = paths;
        _contents = contents;
        _nodes = nodes;
    }

    public async Task<Response<KnowledgePathStepListDto>> Handle(
        GetKnowledgePathStepsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgePathStepListDto>.Fail("Tenant context is required.", 400);
        }

        var path = await _paths.GetByIdAsync(tenantId, request.PathId, cancellationToken);
        if (path is null)
        {
            return Response<KnowledgePathStepListDto>.Fail("Knowledge path not found.", 404);
        }

        var effectiveAt = request.EffectiveAt ?? DateTimeOffset.UtcNow;
        var ctx = new KnowledgePathMapper.ResolutionContext(
            await _contents.ListAsync(tenantId, cancellationToken),
            await _nodes.ListAsync(tenantId, cancellationToken));

        var steps = KnowledgePathMapper.ToStepDtos(path, ctx, effectiveAt, request.IncludeArchived);
        return Response<KnowledgePathStepListDto>.Success(new KnowledgePathStepListDto(steps, steps.Count));
    }
}
