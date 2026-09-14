using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.RehireRecommendations.Handlers;

public sealed class GetRehireRecommendationReadinessListHandler
    : IRequestHandler<GetRehireRecommendationReadinessListQuery, Response<IReadOnlyList<RehireRecommendationReadinessListItemDto>>>
{
    private readonly ITepRehireRecommendationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetRehireRecommendationReadinessListHandler(
        ITepRehireRecommendationReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<RehireRecommendationReadinessListItemDto>>> Handle(
        GetRehireRecommendationReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = RehireRecommendationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<RehireRecommendationReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<RehireRecommendationReadinessListItemDto>>.Success(
            items.Select(RehireRecommendationMapper.ToListItemDto).ToList());
    }
}

public sealed class GetRehireRecommendationReadinessByIdHandler
    : IRequestHandler<GetRehireRecommendationReadinessByIdQuery, Response<RehireRecommendationReadinessDto>>
{
    private readonly ITepRehireRecommendationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetRehireRecommendationReadinessByIdHandler(
        ITepRehireRecommendationReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<RehireRecommendationReadinessDto>> Handle(GetRehireRecommendationReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = RehireRecommendationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RehireRecommendationReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<RehireRecommendationReadinessDto>.Fail("Rehire recommendation readiness record was not found.", 404)
            : Response<RehireRecommendationReadinessDto>.Success(RehireRecommendationMapper.ToDto(item));
    }
}

public sealed class GetRehireRecommendationAuditMetadataHandler
    : IRequestHandler<GetRehireRecommendationAuditMetadataQuery, Response<RehireRecommendationAuditMetadataDto>>
{
    private readonly ITepRehireRecommendationReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetRehireRecommendationAuditMetadataHandler(
        ITepRehireRecommendationReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<RehireRecommendationAuditMetadataDto>> Handle(GetRehireRecommendationAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = RehireRecommendationGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<RehireRecommendationAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<RehireRecommendationAuditMetadataDto>.Fail("Rehire recommendation readiness record was not found.", 404)
            : Response<RehireRecommendationAuditMetadataDto>.Success(new RehireRecommendationAuditMetadataDto(
                item.Id,
                item.ExplainabilityState,
                item.HumanReviewState,
                item.ContestabilityState,
                item.AbuseControlState,
                item.MisuseDetectionState,
                item.ThrottlingState,
                item.EscalationState,
                item.EvidenceRetentionState,
                item.AuditReadinessState,
                item.LegalHoldState,
                item.DeletionPolicyState,
                item.CandidateResponseBoundaryState,
                item.DependencyStates.Select(RehireRecommendationMapper.ToDto).ToList(),
                item.DeferredReason));
    }
}
