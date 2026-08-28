using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.Content.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;

namespace Diten.CrmService.Application.Features.Knowledge.Content.Handlers;

public sealed class ListKnowledgeContentHandler
    : IRequestHandler<ListKnowledgeContentQuery, Response<KnowledgeContentListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgeContentRepository _repository;

    public ListKnowledgeContentHandler(ITenantContext tenant, IKnowledgeContentRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<KnowledgeContentListDto>> Handle(
        ListKnowledgeContentQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgeContentListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<KnowledgeContent> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.ContentType))
        {
            var type = KnowledgeContentTypes.Normalize(request.ContentType);
            rows = rows.Where(c => c.ContentType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.ContentStatus))
        {
            var status = KnowledgeContentStatuses.Normalize(request.ContentStatus);
            rows = rows.Where(c => c.ContentStatus == status);
        }

        if (request.SubjectId is { } subjectId && subjectId != Guid.Empty)
        {
            rows = rows.Where(c => c.SubjectId == subjectId);
        }

        if (request.TopicId is { } topicId && topicId != Guid.Empty)
        {
            rows = rows.Where(c => c.TopicId == topicId);
        }

        if (request.AudienceProfileId is { } profileId && profileId != Guid.Empty)
        {
            rows = rows.Where(c => c.AudienceProfileId == profileId);
        }

        if (!string.IsNullOrWhiteSpace(request.LanguageCode))
        {
            var language = request.LanguageCode.Trim();
            rows = rows.Where(c => string.Equals(c.LanguageCode, language, StringComparison.OrdinalIgnoreCase));
        }

        if (request.BrandId is { } brandId && brandId != Guid.Empty)
        {
            rows = rows.Where(c => c.BrandId == brandId);
        }

        if (request.ProductId is { } productId && productId != Guid.Empty)
        {
            rows = rows.Where(c => c.ProductId == productId);
        }

        if (request.CampaignId is { } campaignId && campaignId != Guid.Empty)
        {
            rows = rows.Where(c => c.CampaignId == campaignId);
        }

        if (request.EffectiveAt is { } at)
        {
            rows = rows.Where(c => c.EffectiveFrom <= at && (c.EffectiveTo is null || at <= c.EffectiveTo));
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(c =>
                c.ContentTitle.Contains(term, StringComparison.OrdinalIgnoreCase)
                || c.ContentCode.Contains(term, StringComparison.OrdinalIgnoreCase)
                || (c.Summary?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(c => !c.IsArchived());
        }

        var items = rows.Select(KnowledgeMapper.ToDto).ToList();
        return Response<KnowledgeContentListDto>.Success(new KnowledgeContentListDto(items, items.Count));
    }
}

public sealed class GetKnowledgeContentHandler
    : IRequestHandler<GetKnowledgeContentQuery, Response<KnowledgeContentDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IKnowledgeContentRepository _repository;

    public GetKnowledgeContentHandler(ITenantContext tenant, IKnowledgeContentRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<KnowledgeContentDto>> Handle(
        GetKnowledgeContentQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<KnowledgeContentDto>.Fail("Tenant context is required.", 400);
        }

        var content = await _repository.GetByIdAsync(tenantId, request.ContentId, cancellationToken);
        return content is null
            ? Response<KnowledgeContentDto>.Fail("Knowledge content not found.", 404)
            : Response<KnowledgeContentDto>.Success(KnowledgeMapper.ToDto(content));
    }
}
