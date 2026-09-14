using Diten.TalentEcosystemService.Application.Common;
using Diten.TalentEcosystemService.Application.Contracts;
using Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Queries;
using Diten.TalentEcosystemService.Domain.Repositories;
using MediatR;

namespace Diten.TalentEcosystemService.Application.Features.CandidateDisputes.Handlers;

public sealed class GetCandidateDisputeReadinessListHandler
    : IRequestHandler<GetCandidateDisputeReadinessListQuery, Response<IReadOnlyList<CandidateDisputeReadinessListItemDto>>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidateDisputeReadinessListHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<IReadOnlyList<CandidateDisputeReadinessListItemDto>>> Handle(
        GetCandidateDisputeReadinessListQuery request,
        CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<IReadOnlyList<CandidateDisputeReadinessListItemDto>>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var items = await _repository.ListAsync(tenant.Data, scope, ct);
        return Response<IReadOnlyList<CandidateDisputeReadinessListItemDto>>.Success(
            items.Select(CandidateDisputeMapper.ToListItemDto).ToList());
    }
}

public sealed class GetCandidateDisputeReadinessByIdHandler
    : IRequestHandler<GetCandidateDisputeReadinessByIdQuery, Response<CandidateDisputeReadinessDto>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidateDisputeReadinessByIdHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidateDisputeReadinessDto>> Handle(GetCandidateDisputeReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateDisputeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<CandidateDisputeReadinessDto>.Fail("Candidate dispute readiness record was not found.", 404)
            : Response<CandidateDisputeReadinessDto>.Success(CandidateDisputeMapper.ToDto(item));
    }
}

public sealed class GetCandidateDisputeAuditMetadataHandler
    : IRequestHandler<GetCandidateDisputeAuditMetadataQuery, Response<CandidateDisputeAuditMetadataDto>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;
    private readonly ILegalEntityContext _legalEntityContext;

    public GetCandidateDisputeAuditMetadataHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext,
        ILegalEntityContext legalEntityContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
        _legalEntityContext = legalEntityContext;
    }

    public async Task<Response<CandidateDisputeAuditMetadataDto>> Handle(GetCandidateDisputeAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateDisputeAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var scope = await _legalEntityContext.GetEffectiveLegalEntityIdsAsync(ct);
        var item = await _repository.GetByIdAsync(tenant.Data, scope, request.Id, ct);
        return item is null
            ? Response<CandidateDisputeAuditMetadataDto>.Fail("Candidate dispute readiness record was not found.", 404)
            : Response<CandidateDisputeAuditMetadataDto>.Success(new CandidateDisputeAuditMetadataDto(
                item.Id,
                item.HumanReviewState,
                item.ContestabilityState,
                item.ResponseBoundaryState,
                item.DisputeIntakeState,
                item.DisputeReviewState,
                item.ResolutionLifecycleState,
                item.EvidenceRetentionState,
                item.AuditReadinessState,
                item.LegalHoldState,
                item.DeletionPolicyState,
                item.SelfServiceBoundaryState,
                item.NotificationDependencyState,
                item.DocumentDependencyState,
                item.AutomatedDecisionBoundaryState,
                item.MarketplaceBoundaryState,
                item.DependencyStates.Select(CandidateDisputeMapper.ToDto).ToList(),
                item.DeferredReason));
    }
}
