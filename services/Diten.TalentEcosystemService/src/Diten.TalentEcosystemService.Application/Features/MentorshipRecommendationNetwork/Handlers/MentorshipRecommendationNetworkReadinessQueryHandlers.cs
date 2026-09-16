using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.MentorshipRecommendationNetwork.Handlers;

public sealed class GetMentorshipRecommendationNetworkReadinessListHandler
    : IRequestHandler<GetMentorshipRecommendationNetworkReadinessListQuery, Response<IReadOnlyList<MentorshipRecommendationNetworkReadinessListItemDto>>>
{
    private readonly IMentorshipRecommendationNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetMentorshipRecommendationNetworkReadinessListHandler(IMentorshipRecommendationNetworkReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<MentorshipRecommendationNetworkReadinessListItemDto>>> Handle(
        GetMentorshipRecommendationNetworkReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = MentorshipRecommendationNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<MentorshipRecommendationNetworkReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var rows = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<MentorshipRecommendationNetworkReadinessListItemDto>>.Success(rows.Select(MentorshipRecommendationNetworkMapper.ToListItem).ToList());
    }
}

public sealed class GetMentorshipRecommendationNetworkReadinessByIdHandler
    : IRequestHandler<GetMentorshipRecommendationNetworkReadinessByIdQuery, Response<MentorshipRecommendationNetworkReadinessDto>>
{
    private readonly IMentorshipRecommendationNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetMentorshipRecommendationNetworkReadinessByIdHandler(IMentorshipRecommendationNetworkReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<MentorshipRecommendationNetworkReadinessDto>> Handle(GetMentorshipRecommendationNetworkReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = MentorshipRecommendationNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MentorshipRecommendationNetworkReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<MentorshipRecommendationNetworkReadinessDto>.Fail("MentorshipRecommendationNetwork readiness record was not found.", 404)
            : Response<MentorshipRecommendationNetworkReadinessDto>.Success(MentorshipRecommendationNetworkMapper.ToDto(entity));
    }
}

public sealed class GetMentorshipRecommendationNetworkAuditMetadataHandler
    : IRequestHandler<GetMentorshipRecommendationNetworkAuditMetadataQuery, Response<MentorshipRecommendationNetworkAuditMetadataDto>>
{
    private readonly IMentorshipRecommendationNetworkReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetMentorshipRecommendationNetworkAuditMetadataHandler(IMentorshipRecommendationNetworkReadinessMetadataRepository repository, ITenantContext tenantContext, ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<MentorshipRecommendationNetworkAuditMetadataDto>> Handle(GetMentorshipRecommendationNetworkAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = MentorshipRecommendationNetworkGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<MentorshipRecommendationNetworkAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var entity = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return entity is null
            ? Response<MentorshipRecommendationNetworkAuditMetadataDto>.Fail("MentorshipRecommendationNetwork readiness record was not found.", 404)
            : Response<MentorshipRecommendationNetworkAuditMetadataDto>.Success(MentorshipRecommendationNetworkMapper.ToAuditMetadata(entity));
    }
}
