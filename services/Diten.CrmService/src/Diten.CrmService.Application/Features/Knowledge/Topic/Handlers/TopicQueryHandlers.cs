using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Topic.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using TopicEntity = Diten.CrmService.Domain.Entities.Topic;

namespace Diten.CrmService.Application.Features.Knowledge.Topic.Handlers;

public sealed class ListTopicsHandler : IRequestHandler<ListTopicsQuery, Response<TopicListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITopicRepository _repository;

    public ListTopicsHandler(ITenantContext tenant, ITopicRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<TopicListDto>> Handle(ListTopicsQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TopicListDto>.Fail("Tenant context is required.", 400);
        }

        IReadOnlyList<TopicEntity> source = request.SubjectId is { } subjectId && subjectId != Guid.Empty
            ? await _repository.ListBySubjectAsync(tenantId, subjectId, cancellationToken)
            : await _repository.ListAsync(tenantId, cancellationToken);

        IEnumerable<TopicEntity> rows = source;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TaxonomyStatuses.Normalize(request.Status);
            rows = rows.Where(t => t.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(t =>
                t.TopicName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || t.TopicCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(t => !t.IsArchived());
        }

        var items = rows.Select(KnowledgeMapper.ToDto).ToList();
        return Response<TopicListDto>.Success(new TopicListDto(items, items.Count));
    }
}

public sealed class GetTopicHandler : IRequestHandler<GetTopicQuery, Response<TopicDto>>
{
    private readonly ITenantContext _tenant;
    private readonly ITopicRepository _repository;

    public GetTopicHandler(ITenantContext tenant, ITopicRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<TopicDto>> Handle(GetTopicQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<TopicDto>.Fail("Tenant context is required.", 400);
        }

        var topic = await _repository.GetByIdAsync(tenantId, request.TopicId, cancellationToken);
        return topic is null
            ? Response<TopicDto>.Fail("Topic not found.", 404)
            : Response<TopicDto>.Success(KnowledgeMapper.ToDto(topic));
    }
}
