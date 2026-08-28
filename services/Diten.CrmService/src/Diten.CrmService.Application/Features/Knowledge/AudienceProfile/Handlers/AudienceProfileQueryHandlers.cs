using Diten.CrmService.Application.Common;
using Diten.CrmService.Application.Common.Models;
using Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Queries;
using Diten.CrmService.Domain.Entities;
using Diten.CrmService.Domain.Repositories;
using MediatR;
using AudienceProfileEntity = Diten.CrmService.Domain.Entities.AudienceProfile;

namespace Diten.CrmService.Application.Features.Knowledge.AudienceProfile.Handlers;

public sealed class ListAudienceProfilesHandler
    : IRequestHandler<ListAudienceProfilesQuery, Response<AudienceProfileListDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAudienceProfileRepository _repository;

    public ListAudienceProfilesHandler(ITenantContext tenant, IAudienceProfileRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<AudienceProfileListDto>> Handle(
        ListAudienceProfilesQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AudienceProfileListDto>.Fail("Tenant context is required.", 400);
        }

        IEnumerable<AudienceProfileEntity> rows = await _repository.ListAsync(tenantId, cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            var status = TaxonomyStatuses.Normalize(request.Status);
            rows = rows.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(request.ProfileType))
        {
            var type = AudienceProfileTypes.Normalize(request.ProfileType);
            rows = rows.Where(p => p.ProfileType == type);
        }

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            rows = rows.Where(p =>
                p.ProfileName.Contains(term, StringComparison.OrdinalIgnoreCase)
                || p.ProfileCode.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!request.IncludeArchived)
        {
            rows = rows.Where(p => !p.IsArchived());
        }

        var items = rows.Select(KnowledgeMapper.ToDto).ToList();
        return Response<AudienceProfileListDto>.Success(new AudienceProfileListDto(items, items.Count));
    }
}

public sealed class GetAudienceProfileHandler
    : IRequestHandler<GetAudienceProfileQuery, Response<AudienceProfileDto>>
{
    private readonly ITenantContext _tenant;
    private readonly IAudienceProfileRepository _repository;

    public GetAudienceProfileHandler(ITenantContext tenant, IAudienceProfileRepository repository)
    {
        _tenant = tenant;
        _repository = repository;
    }

    public async Task<Response<AudienceProfileDto>> Handle(
        GetAudienceProfileQuery request, CancellationToken cancellationToken)
    {
        if (_tenant.TenantId is not { } tenantId)
        {
            return Response<AudienceProfileDto>.Fail("Tenant context is required.", 400);
        }

        var profile = await _repository.GetByIdAsync(tenantId, request.AudienceProfileId, cancellationToken);
        return profile is null
            ? Response<AudienceProfileDto>.Fail("Audience profile not found.", 404)
            : Response<AudienceProfileDto>.Success(KnowledgeMapper.ToDto(profile));
    }
}
