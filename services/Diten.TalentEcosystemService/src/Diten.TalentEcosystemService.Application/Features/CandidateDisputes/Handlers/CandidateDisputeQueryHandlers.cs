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

    public GetCandidateDisputeReadinessListHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
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

        var items = await _repository.ListAsync(tenant.Data, ct);
        return Response<IReadOnlyList<CandidateDisputeReadinessListItemDto>>.Success(
            items.Select(CandidateDisputeMapper.ToListItemDto).ToList());
    }
}

public sealed class GetCandidateDisputeReadinessByIdHandler
    : IRequestHandler<GetCandidateDisputeReadinessByIdQuery, Response<CandidateDisputeReadinessDto>>
{
    private readonly ITepCandidateDisputeReadinessMetadataRepository _repository;
    private readonly ITenantContext _tenantContext;

    public GetCandidateDisputeReadinessByIdHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<CandidateDisputeReadinessDto>> Handle(GetCandidateDisputeReadinessByIdQuery request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateDisputeReadinessDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var item = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
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

    public GetCandidateDisputeAuditMetadataHandler(
        ITepCandidateDisputeReadinessMetadataRepository repository,
        ITenantContext tenantContext)
    {
        _repository = repository;
        _tenantContext = tenantContext;
    }

    public async Task<Response<CandidateDisputeAuditMetadataDto>> Handle(GetCandidateDisputeAuditMetadataQuery request, CancellationToken ct)
    {
        var tenant = CandidateDisputeGuard.RequireTenant(_tenantContext);
        if (!tenant.IsSuccessful)
        {
            return Response<CandidateDisputeAuditMetadataDto>.Fail(tenant.Errors, tenant.StatusCode);
        }

        var item = await _repository.GetByIdAsync(tenant.Data, request.Id, ct);
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
